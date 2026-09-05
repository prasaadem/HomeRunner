using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
        readonly Color[] colors = { new Color(.3f,.65f,.58f), new Color(.38f,.47f,.6f), new Color(.75f,.48f,.32f), new Color(.5f,.4f,.7f), new Color(.25f,.3f,.5f) };
        Transform runner, body, leftArm, rightArm, leftLeg, rightLeg;
        Camera view;
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
            view.farClipPlane = 180;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.075f,.09f,.14f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45,-25,0);
            RenderSettings.ambientLight = new Color(.6f,.62f,.68f);
            CreateRunner();
            ResetRun();
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
        void Label(Transform parent, string title, Vector3 p)
        {
            var g = new GameObject(title); g.transform.SetParent(parent,false);
            g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0,180,0);
            var text = g.AddComponent<TextMesh>(); text.text = title;
            text.anchor = TextAnchor.MiddleCenter; text.characterSize = .17f; text.fontSize = 48;
            text.color = Color.white;
        }
        void CreateRunner()
        {
            runner = new GameObject("Original articulated runner").transform;
            body = new GameObject("Body").transform; body.SetParent(runner,false);
            Color teal = new Color(.1f,.8f,.73f), skin = new Color(.73f,.48f,.32f), navy = new Color(.1f,.14f,.24f);
            Shape(body,"Jacket",new Vector3(0,1.25f,0),new Vector3(.65f,.7f,.4f),teal);
            Shape(body,"Head",new Vector3(0,1.9f,0),Vector3.one*.48f,skin,PrimitiveType.Sphere);
            Shape(body,"Hair",new Vector3(0,2.08f,-.02f),new Vector3(.49f,.2f,.46f),navy,PrimitiveType.Sphere);
            Shape(body,"Backpack",new Vector3(0,1.3f,-.3f),new Vector3(.48f,.55f,.25f),new Color(1,.65f,.15f));
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
                Label(room,themes[theme]+"  /  "+(f+1),new Vector3(0,3.15f,23.5f));
                for (int l=-1;l<=1;l++)
                {
                    int dest = Mathf.Clamp(f+l,0,2);
                    // Lane -1 descends, lane +1 ascends; center stays level.
                    for (int step=0;step<32;step++)
                    {
                        float t = (step+.5f)/32f;
                        Shape(room,"Stair tread",new Vector3(l*2.6f,(dest-f)*Height*t-.1f,24+t*16),
                            new Vector3(2.55f,.2f,.5f),l==0?colors[theme]*.7f:new Color(.8f,.62f,.24f));
                    }
                    Label(room,dest<f?"DOWN":dest>f?"UP":"STAY",new Vector3(l*2.6f,2.6f,23));
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
                        kind==0?new Color(1,.55f,.15f):kind==1?new Color(.9f,.25f,.5f):new Color(.85f,.2f,.18f));
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
                if(k.enterKey.wasPressedThisFrame) started=true;
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
            body.localScale=new Vector3(1,slide>0?.45f:1,1);
            float swing=jump>0?15:Mathf.Sin(phase)*35;
            leftLeg.localRotation=Quaternion.Euler(swing,0,0); rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
            leftArm.localRotation=Quaternion.Euler(-swing,0,-8); rightArm.localRotation=Quaternion.Euler(swing,0,8);
            body.localRotation=Quaternion.Euler(dead?65:slide>0?15:0,0,(x-lane*2.6f)*-5);
            Vector3 desired=runner.position+new Vector3(-x*.6f,3.4f,-7);
            // Origin shifts happen by exactly one room, so snap z to prevent a camera sweep.
            Vector3 cameraPos=view.transform.position;
            if(Mathf.Abs(cameraPos.z-desired.z)>Length*.5f) cameraPos.z-=Length;
            view.transform.position=Vector3.Lerp(cameraPos,desired,1-Mathf.Exp(-8*dt));
            view.transform.LookAt(runner.position+Vector3.up*1.3f+Vector3.forward*4);
        }
        void OnGUI()
        {
            GUI.skin.label.fontSize=20; GUI.skin.button.fontSize=20;
            GUI.Box(new Rect(16,16,440,150),"");
            GUILayout.BeginArea(new Rect(30,25,415,135));
            GUILayout.Label("HOME RUNNER  /  Floor "+(floor+1)+" of 3");
            GUILayout.Label("Distance "+(int)distance+" m   |   Best "+best+" m");
            GUILayout.Label(themes[(currentChunk*3+floor)%5]);
            GUILayout.Label(locked?"Route committed":"Stairs: left DOWN / middle STAY / right UP");
            GUILayout.EndArea();
            GUI.Label(new Rect(20,Screen.height-65,900,60),"A/D or arrows: lanes   SPACE: jump   S: slide   ESC: pause   R: restart");
            if(!started||dead||paused)
            {
                var box=new Rect(Screen.width/2-210,Screen.height/2-105,420,210);
                GUI.Box(box,"");
                GUI.Label(new Rect(box.x+25,box.y+25,380,70),dead?"Run ended!":paused?"Paused":"Welcome home. How far can you run?");
                if(GUI.Button(new Rect(box.x+50,box.y+110,320,50),dead?"Try again":paused?"Resume":"Start running"))
                {
                    if(dead) ResetRun();
                    paused=false; started=true;
                }
            }
        }
        void OnDestroy()
        {
            foreach(var material in materials.Values) Destroy(material);
        }
    }
}
