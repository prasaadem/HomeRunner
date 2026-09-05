using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HomeRunner.Editor
{
    public static class HomeRunnerArtSetup
    {
        const string ModelPath = "Assets/ThirdParty/Quaternius/Runner.fbx";
        const string Output = "Assets/Resources/HomeRunner";
        [MenuItem("HomeRunner/Build Character and Art")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before building art."); return; }
            if(!File.Exists(ModelPath))
            {
                using(var source=File.OpenRead(ModelPath+".gz.bytes"))
                using(var gzip=new GZipStream(source,CompressionMode.Decompress))
                using(var output=File.Create(ModelPath)) gzip.CopyTo(output);
                AssetDatabase.ImportAsset(ModelPath,ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Runner.fbx has not imported yet.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("|Run") || clip.name.EndsWith("|Idle") || clip.name.EndsWith("|Idle_Neutral");
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.lockRootRotation = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var all = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Find(string name) => all.FirstOrDefault(c=>c.name.EndsWith("|"+name) || c.name==name);
            var idle=Find("Idle_Neutral") ?? Find("Idle");
            var run=Find("Run"); var death=Find("Death"); var roll=Find("Roll");
            if(idle==null || run==null) throw new InvalidOperationException("Missing Idle/Run clips. Imported clips: "+string.Join(", ",all.Select(c=>c.name)));
            string controllerPath=Output+"/Runner.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            else foreach(var layer in controller.layers) foreach(var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sliding",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;
            var idleState=machine.AddState("Idle"); idleState.motion=idle;
            var runState=machine.AddState("Run"); runState.motion=run;
            machine.defaultState=idleState;
            var go=idleState.AddTransition(runState); go.hasExitTime=false; go.duration=.15f; go.AddCondition(AnimatorConditionMode.Greater,.1f,"Speed");
            var stop=runState.AddTransition(idleState); stop.hasExitTime=false; stop.duration=.15f; stop.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");
            if(death!=null)
            {
                var state=machine.AddState("Defeat"); state.motion=death;
                var t=machine.AddAnyStateTransition(state); t.hasExitTime=false; t.duration=.1f; t.canTransitionToSelf=false; t.AddCondition(AnimatorConditionMode.If,0,"Dead");
                var back=state.AddTransition(idleState); back.hasExitTime=false; back.duration=.1f; back.AddCondition(AnimatorConditionMode.IfNot,0,"Dead");
            }
            if(roll!=null)
            {
                var state=machine.AddState("Roll under obstacle"); state.motion=roll;
                var t=runState.AddTransition(state); t.hasExitTime=false; t.duration=.08f; t.AddCondition(AnimatorConditionMode.If,0,"Sliding");
                var back=state.AddTransition(runState); back.hasExitTime=false; back.duration=.1f; back.AddCondition(AnimatorConditionMode.IfNot,0,"Sliding");
            }
            var root=new GameObject("Runner");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(root.transform,false);
                var animator=model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
                var renderers=model.GetComponentsInChildren<Renderer>();
                if(renderers.Length==0) throw new InvalidOperationException("Character has no renderers.");
                var bounds=renderers[0].bounds; foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                float scale=1.8f/Mathf.Max(.01f,bounds.size.y);
                model.transform.localScale*=scale;
                model.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/Runner.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Character prefab created with authored Idle, Run, Roll and Defeat clips. Jump and stair-specific animation remain to be authored. Press Play to inspect.");
        }
    }
}
