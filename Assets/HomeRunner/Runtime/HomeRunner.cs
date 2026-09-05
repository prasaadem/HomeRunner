using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HomeRunner
{
    // Prototype: explicit route geometry and analytical collision, no NavMesh required.
    public sealed class HomeRunner : MonoBehaviour
    {
        const float Length = 48f, Height = 6f;
        readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Hazard> hazards = new List<Hazard>();
        readonly string[] themes = { "KITCHEN", "GARAGE", "LIVING ROOM", "GYM", "MEDIA ROOM" };
        readonly Color[] colors = { new Color(.67f,.64f,.58f), new Color(.38f,.40f,.41f), new Color(.56f,.49f,.41f), new Color(.46f,.49f,.47f), new Color(.27f,.29f,.32f) };
        Transform runner, body, leftArm, rightArm, leftLeg, rightLeg;
        Camera view;
        Canvas hud;
        Text scoreText, roomText, routeText, modalTitle, modalDetail, actionText;
        GameObject modal;
        Font uiFont;
        Animator characterAnimator;
        readonly HashSet<string> animationParameters = new HashSet<string>();
        float distance, x, jump, velocity, slide, phase;
        int floor, targetFloor, lane, currentChunk, best;
        bool locked, dead, paused, started;
        struct Hazard { public int chunk, floor, lane, kind; public float z; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (FindFirstObjectByType<HomeRunner>() == null)
                new GameObject("HomeRunner").AddComponent<HomeRunner>();
        }
        void Start()
        {
            Application.targetFrameRate = 60;
            best = PlayerPrefs.GetInt("HomeRunner.Best", 0);
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.gameObject.SetActive(false);
            view = new GameObject("Runner Camera").AddComponent<Camera>();
            view.fieldOfView = 65;
            view.allowMSAA = true;
            QualitySettings.antiAliasing = 4;
            view.farClipPlane = 180;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.075f,.09f,.14f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45,-25,0);
            RenderSettings.ambientLight = new Color(.6f,.62f,.68f);
            CreateRunner();
            ResetRun();
            CreateInterface();
        }
        Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var material)) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader); material.color = color;
            materials.Add(color, material); return material;
        }
        Transform Shape(Transform parent, string name, Vector3 p, Vector3 scale, Color color, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name;
            g.transform.SetParent(parent,false); g.transform.localPosition = p; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(color);
            Destroy(g.GetComponent<Collider>());
            return g.transform;
        }
        void CreateRunner()
        {
            runner = new GameObject("Original articulated runner").transform;
            body = new GameObject("Body").transform; body.SetParent(runner,false);
            // Optional production character. Prefab must face +Z with feet at y=0.
            var prefab = Resources.Load<GameObject>("HomeRunner/Runner");
            if (prefab != null)
            {
                var model = Instantiate(prefab, body);
                characterAnimator = model.GetComponentInChildren<Animator>();
                if (characterAnimator != null)
                {
                    characterAnimator.applyRootMotion = false;
                    foreach (var parameter in characterAnimator.parameters)
                        animationParameters.Add(parameter.name);
                }
                return;
            }
            Color teal = new Color(.24f,.29f,.28f), skin = new Color(.73f,.48f,.32f), navy = new Color(.1f,.14f,.24f);
            Shape(body,"Jacket",new Vector3(0,1.25f,0),new Vector3(.65f,.7f,.4f),teal);
            Shape(body,"Head",new Vector3(0,1.9f,0),Vector3.one*.48f,skin,PrimitiveType.Sphere);
            Shape(body,"Hair",new Vector3(0,2.08f,-.02f),new Vector3(.49f,.2f,.46f),navy,PrimitiveType.Sphere);
            Shape(body,"Backpack",new Vector3(0,1.3f,-.3f),new Vector3(.48f,.55f,.25f),new Color(.39f,.31f,.23f));
            leftArm = Limb("Left arm",new Vector3(-.43f,1.5f,0),new Vector3(.22f,.65f,.22f),teal);
            rightArm = Limb("Right arm",new Vector3(.43f,1.5f,0),new Vector3(.22f,.65f,.22f),teal);
            leftLeg = Limb("Left leg",new Vector3(-.19f,.95f,0),new Vector3(.25f,.85f,.27f),navy);
            rightLeg = Limb("Right leg",new Vector3(.19f,.95f,0),new Vector3(.25f,.85f,.27f),navy);
            Shape(leftLeg,"Sneaker",new Vector3(0,-.8f,.1f),new Vector3(.3f,.18f,.48f),Color.white);
            Shape(rightLeg,"Sneaker",new Vector3(0,-.8f,.1f),new Vector3(.3f,.18f,.48f),Color.white);
        }
        Transform Limb(string title, Vector3 p, Vector3 size, Color color)
        {
            var pivot = new GameObject(title).transform; pivot.SetParent(body,false); pivot.localPosition = p;
            Shape(pivot,title+" mesh",new Vector3(0,-size.y/2,0),size,color);
            return pivot;
        }
        void ResetRun()
        {
            foreach (var chunk in chunks.Values) Destroy(chunk);
            chunks.Clear(); hazards.Clear();
            distance=2; x=jump=velocity=slide=phase=0;
            floor=targetFloor=lane=currentChunk=0; locked=dead=paused=started=false;
            for (int i=0;i<5;i++) BuildChunk(i);
            UpdatePose(0); view.transform.position = runner.position + new Vector3(0,3.4f,-7);
            view.transform.LookAt(runner.position+Vector3.up*1.3f+Vector3.forward*4);
        }
        void BuildChunk(int index)
        {
            var root = new GameObject("Section "+index); chunks.Add(index,root);
            // Coordinates stay near origin: current chunk is always at local z=0.
            root.transform.position = new Vector3(0,0,(index-currentChunk)*Length);
            for (int f=0;f<3;f++)
            {
                int theme = (index*3+f)%5;
                var room = new GameObject(themes[theme]+" floor "+(f+1)).transform;
                room.SetParent(root.transform,false); room.localPosition = Vector3.up*f*Height;
                Shape(room,"Room floor",new Vector3(0,-.15f,12),new Vector3(10,.3f,24),colors[theme]*.65f);
                Shape(room,"Exit landing",new Vector3(0,-.15f,44),new Vector3(10,.3f,8),colors[theme]*.65f);
                Shape(room,"Left wall",new Vector3(-5,2,12),new Vector3(.2f,4,24),colors[theme]);
                Shape(room,"Right wall",new Vector3(5,2,12),new Vector3(.2f,4,24),colors[theme]);
                Shape(room,"Door header",new Vector3(0,3.7f,23.8f),new Vector3(10,.5f,.35f),colors[theme]);
                for (int l=-1;l<=1;l++)
                {
                    int dest = Mathf.Clamp(f+l,0,2);
                    // Lane -1 descends, lane +1 ascends; center stays level.
                    for (int step=0;step<32;step++)
                    {
                        float t = (step+.5f)/32f;
                        Shape(room,"Stair tread",new Vector3(l*2.6f,(dest-f)*Height*t-.1f,24+t*16),
                            new Vector3(2.55f,.2f,.5f),l==0?colors[theme]*.7f:new Color(.48f,.42f,.34f));
                    }
                }
                for (int side=-1;side<=1;side+=2)
                    for (int n=0;n<3;n++) Decorate(room,theme,side*4.1f,4+n*6);
                // One occupied lane per row: two guaranteed open lanes.
                if(index>0) for(int row=0;row<2;row++)
                {
                    int l = ((index+f+row)%3)-1, kind=(index+row)%3;
                    float z=9+row*7;
                    var h = new Hazard {chunk=index,floor=f,lane=l,kind=kind,z=index*Length+z};
                    hazards.Add(h);
                    Shape(room,kind==0?"Jump crate":kind==1?"Slide bar":"Dodge cabinet",
                        new Vector3(l*2.6f,kind==0?.4f:kind==1?1.65f:1.1f,z),
                        new Vector3(1.8f,kind==0?.8f:kind==1?.35f:2.2f,.7f),
                        kind==0?new Color(.68f,.46f,.23f):kind==1?new Color(.55f,.34f,.25f):new Color(.48f,.22f,.19f));
                }
            }
        }
        void Decorate(Transform room,int theme,float xPos,float z)
        {
            Color dark=new Color(.16f,.19f,.25f), white=new Color(.85f,.86f,.8f);
            if(theme==0)
            {
                Shape(room,"Kitchen cabinet",new Vector3(xPos,.6f,z),new Vector3(1.2f,1.2f,2),white);
                Shape(room,"Countertop",new Vector3(xPos,1.25f,z),new Vector3(1.3f,.12f,2.1f),dark);
                Shape(room,"Cooking pot",new Vector3(xPos,1.5f,z),new Vector3(.55f,.4f,.55f),dark,PrimitiveType.Cylinder);
            }
            else if(theme==1)
            {
                Shape(room,"Tool chest",new Vector3(xPos,.65f,z),new Vector3(1.1f,1.3f,1.4f),new Color(.7f,.15f,.1f));
                for(int n=0;n<3;n++) Shape(room,"Tire stack",new Vector3(xPos,.2f+n*.3f,z+1.8f),new Vector3(.8f,.15f,.8f),dark,PrimitiveType.Cylinder);
            }
            else if(theme==2)
            {
                Shape(room,"Sofa base",new Vector3(xPos,.4f,z),new Vector3(1.2f,.7f,2),colors[2]);
                Shape(room,"Sofa back",new Vector3(xPos+Mathf.Sign(xPos)*.4f,1,z),new Vector3(.3f,1,2),colors[2]);
                Shape(room,"Lamp",new Vector3(xPos,1.5f,z+2),new Vector3(.6f,.55f,.6f),white,PrimitiveType.Cylinder);
                Shape(room,"Lamp stand",new Vector3(xPos,.7f,z+2),new Vector3(.08f,1.4f,.08f),dark);
            }
            else if(theme==3)
            {
                Shape(room,"Treadmill",new Vector3(xPos,.2f,z),new Vector3(1.1f,.3f,2),dark);
                Shape(room,"Treadmill console",new Vector3(xPos,1.1f,z+.8f),new Vector3(1,.2f,.4f),colors[3]);
                Shape(room,"Exercise ball",new Vector3(xPos,.5f,z+2),Vector3.one*.9f,colors[3],PrimitiveType.Sphere);
            }
            else
            {
                Shape(room,"Speaker",new Vector3(xPos,1,z),new Vector3(.8f,2,.7f),dark);
                Shape(room,"Screen",new Vector3(xPos,2,z+1),new Vector3(.15f,1.6f,2.4f),new Color(.2f,.6f,.9f));
                Shape(room,"Media console",new Vector3(xPos,.4f,z+1),new Vector3(1,.6f,2.4f),dark);
            }
        }
        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.rKey.wasPressedThisFrame) { ResetRun(); return; }
                if(k.escapeKey.wasPressedThisFrame) paused=!paused;
                if((k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) && (!started || dead || paused)) ActivateRun();
                if(!locked && !dead && !paused)
                {
                    if(k.aKey.wasPressedThisFrame||k.leftArrowKey.wasPressedThisFrame) lane=Mathf.Max(-1,lane-1);
                    if(k.dKey.wasPressedThisFrame||k.rightArrowKey.wasPressedThisFrame) lane=Mathf.Min(1,lane+1);
                }
                if(!dead&&!paused&&started)
                {
                    if(k.spaceKey.wasPressedThisFrame&&jump<=0&&slide<=0) velocity=8;
                    if(k.sKey.wasPressedThisFrame&&jump<=0) slide=.85f;
                }
            }
            if(dead||paused||!started) return;
            // Cap delta to avoid tunnelling after a long frame; simulation slows rather than skips.
            float dt=Mathf.Min(Time.deltaTime,.04f);
            float speed=Mathf.Min(13,7+distance/600);
            float old=distance;
            distance+=speed*dt;
            float local=distance-currentChunk*Length;
            if(!locked&&local>=23)
            {
                locked=true; targetFloor=Mathf.Clamp(floor+lane,0,2);
            }
            if(local>=Length)
            {
                floor=targetFloor; locked=false; currentChunk++;
                foreach(var pair in chunks) pair.Value.transform.position=new Vector3(0,0,(pair.Key-currentChunk)*Length);
                int expired=currentChunk-2;
                if(chunks.TryGetValue(expired,out var g)) { Destroy(g); chunks.Remove(expired); hazards.RemoveAll(h=>h.chunk==expired); }
                BuildChunk(currentChunk+4);
            }
            x=Mathf.MoveTowards(x,lane*2.6f,dt*16);
            if(jump>0||velocity>0) { velocity-=22*dt; jump=Mathf.Max(0,jump+velocity*dt); }
            slide=Mathf.Max(0,slide-dt);
            foreach(var h in hazards)
            {
                if(h.floor!=floor||Mathf.Abs(x-h.lane*2.6f)>1.15f) continue;
                if(distance+.35f<h.z-.35f||old-.35f>h.z+.35f) continue;
                if(h.kind==0&&jump>.85f||h.kind==1&&slide>0) continue;
                dead=true;
                best=Mathf.Max(best,(int)distance);
                PlayerPrefs.SetInt("HomeRunner.Best",best); PlayerPrefs.Save(); break;
            }
            phase+=dt*speed*1.4f; UpdatePose(dt);
        }
        void UpdatePose(float dt)
        {
            float local=distance-currentChunk*Length;
            float y=Mathf.Lerp(floor*Height,targetFloor*Height,Mathf.Clamp01((local-24)/16));
            runner.position=new Vector3(x,y+jump,local);
            body.localScale=new Vector3(1,characterAnimator == null && slide>0?.45f:1,1);
            float swing=jump>0?15:Mathf.Sin(phase)*35;
            if (leftLeg != null)
            {
            leftLeg.localRotation=Quaternion.Euler(swing,0,0); rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
            leftArm.localRotation=Quaternion.Euler(-swing,0,-8); rightArm.localRotation=Quaternion.Euler(swing,0,8);
            }
            body.localRotation=Quaternion.Euler(dead?65:slide>0?15:0,0,(x-lane*2.6f)*-5);
            Vector3 desired=runner.position+new Vector3(-x*.6f,3.4f,-7);
            // Origin shifts happen by exactly one room, so snap z to prevent a camera sweep.
            Vector3 cameraPos=view.transform.position;
            if(Mathf.Abs(cameraPos.z-desired.z)>Length*.5f) cameraPos.z-=Length;
            view.transform.position=Vector3.Lerp(cameraPos,desired,1-Mathf.Exp(-8*dt));
            view.transform.LookAt(runner.position+Vector3.up*1.3f+Vector3.forward*4);
        }
        RectTransform UIBox(Transform parent, string title, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=low; rect.offsetMax=high;
            return rect;
        }
        Text UIText(Transform parent, string title, int size, TextAnchor alignment, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            var t=UIBox(parent,title,min,max,low,high).gameObject.AddComponent<Text>();
            t.text=title; t.font=uiFont; t.fontSize=size; t.alignment=alignment;
            t.color=new Color(.94f,.95f,.96f); t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;
            return t;
        }
        void CreateInterface()
        {
            uiFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root=new GameObject("HomeRunner Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);
            hud=root.GetComponent<Canvas>(); hud.renderMode=RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var events=FindFirstObjectByType<EventSystem>();
            if(events == null)
            {
                var eventObject=new GameObject("HomeRunner Events",typeof(EventSystem));
                eventObject.transform.SetParent(transform,false);
                events=eventObject.GetComponent<EventSystem>();
            }
            // Existing scenes can contain the legacy input module or unbound UI actions.
            foreach(var oldModule in events.GetComponents<BaseInputModule>())
                oldModule.enabled=false;
            var input=events.GetComponent<InputSystemUIInputModule>();
            if(input == null) input=events.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            input.enabled=true;
            events.enabled=true;
            Cursor.lockState=CursorLockMode.None;
            Cursor.visible=true;
            var top=UIBox(root.transform,"Header",new Vector2(0,1),Vector2.one,new Vector2(24,-110),new Vector2(-24,-24));
            top.gameObject.AddComponent<Image>().color=new Color(.065f,.065f,.07f,.96f);
            UIText(top,"HOME / RUNNER",18,TextAnchor.MiddleLeft,Vector2.zero,new Vector2(.5f,1),new Vector2(24,35),new Vector2(0,-8));
            roomText=UIText(top,"Room",24,TextAnchor.MiddleLeft,Vector2.zero,new Vector2(.6f,1),new Vector2(24,5),new Vector2(0,-38));
            scoreText=UIText(top,"Distance",22,TextAnchor.MiddleRight,new Vector2(.6f,0),Vector2.one,new Vector2(0,0),new Vector2(-24,0));
            routeText=UIText(root.transform,"Route",22,TextAnchor.UpperCenter,new Vector2(0,1),Vector2.one,new Vector2(24,-160),new Vector2(-24,-122));
            UIText(root.transform,"A / D  Move     SPACE  Jump     S  Slide     ESC  Pause",18,TextAnchor.LowerCenter,Vector2.zero,new Vector2(1,0),new Vector2(24,24),new Vector2(-24,62));
            var panel=UIBox(root.transform,"Menu",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-260,-155),new Vector2(260,155));
            modal=panel.gameObject;
            modal.AddComponent<Image>().color=new Color(.065f,.065f,.07f,.98f);
            var accent=UIBox(panel,"Accent",new Vector2(0,1),Vector2.one,new Vector2(0,-4),Vector2.zero);
            accent.gameObject.AddComponent<Image>().color=new Color(.72f,.62f,.48f);
            modalTitle=UIText(panel,"Title",40,TextAnchor.MiddleCenter,new Vector2(0,1),Vector2.one,new Vector2(24,-98),new Vector2(-24,-24));
            modalDetail=UIText(panel,"Detail",20,TextAnchor.MiddleCenter,new Vector2(0,1),Vector2.one,new Vector2(24,-172),new Vector2(-24,-98));
            var button=UIBox(panel,"Continue",Vector2.zero,new Vector2(1,0),new Vector2(36,32),new Vector2(-36,96));
            var background=button.gameObject.AddComponent<Image>(); background.color=new Color(.72f,.62f,.48f);
            var action=button.gameObject.AddComponent<Button>(); action.targetGraphic=background;
            action.onClick.AddListener(ActivateRun);
            actionText=UIText(button,"Action",22,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            actionText.color=new Color(.09f,.075f,.06f);
        }
        void ActivateRun()
        {
            if(dead) ResetRun();
            paused=false;
            started=true;
            if(modal != null) modal.SetActive(false);
        }
        void LateUpdate()
        {
            if(hud == null) return;
            roomText.text=themes[(currentChunk*3+floor)%5]+"  /  FLOOR "+(floor+1);
            scoreText.text=((int)distance)+" m   |   BEST "+best+" m";
            float local=distance-currentChunk*Length;
            routeText.text=locked?"FOLLOW YOUR ROUTE":local>15?"LEFT: "+(floor==0?"LEVEL":"DOWN")+"     CENTER: LEVEL     RIGHT: "+(floor==2?"LEVEL":"UP"):"";
            modal.SetActive(!started||dead||paused);
            modalTitle.text=dead?"Run complete":paused?"Take a breath":"HOME RUNNER";
            modalDetail.text=dead?((int)distance)+" meters explored":"Five rooms. Three floors. Keep moving.";
            actionText.text=dead?"RUN AGAIN":paused?"RESUME":"START RUNNING";
            if(characterAnimator != null)
            {
                characterAnimator.speed=paused?0:1;
                if(animationParameters.Contains("Speed")) characterAnimator.SetFloat("Speed",started&&!dead&&!paused?Mathf.Min(13,7+distance/600):0);
                if(animationParameters.Contains("Grounded")) characterAnimator.SetBool("Grounded",jump<=0);
                if(animationParameters.Contains("Sliding")) characterAnimator.SetBool("Sliding",slide>0);
                if(animationParameters.Contains("Dead")) characterAnimator.SetBool("Dead",dead);
            }
        }
        void OnDestroy()
        {
            foreach(var material in materials.Values) Destroy(material);
        }
    }
}
