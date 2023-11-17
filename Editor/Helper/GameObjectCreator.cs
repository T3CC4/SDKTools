#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SDKTools
{
    public class GameObjectCreator : Editor
    {
        [MenuItem("GameObject/Audio/WorldAudio", false, 0)]
        static void CreateWorldAudio(MenuCommand menuCommand)
        {
            GameObject audioGO = new GameObject("AudioGameObject");
            audioGO.transform.SetParent(Selection.activeGameObject?.transform);
            audioGO.transform.localPosition = Vector3.zero;
            audioGO.transform.localRotation = Quaternion.identity;
            audioGO.transform.localScale = Vector3.one;
            AudioSource audio = audioGO.AddComponent<AudioSource>();
            audio.priority = 0;
            audio.reverbZoneMix = 0;
            audio.dopplerLevel = 0;
            audio.minDistance = 990099;
            audio.maxDistance = 1000000;
            audio.rolloffMode = AudioRolloffMode.Custom;
            Selection.activeGameObject = audioGO;
            Undo.RegisterCreatedObjectUndo(audioGO, "Create WorldAudio");
        }

        [MenuItem("GameObject/3D Object/Sphere - Animation", false, 0)]
        static void CreateSphere(MenuCommand menuCommand)
        {
            GameObject sphereGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGO.name = "Animation Sphere";
            sphereGO.transform.SetParent(Selection.activeGameObject?.transform);
            sphereGO.transform.localPosition = Vector3.zero;
            sphereGO.transform.localRotation = Quaternion.identity;
            sphereGO.transform.localScale = new Vector3(50f, 50f, 50f);
            DestroyImmediate(sphereGO.GetComponent<SphereCollider>());
            Selection.activeGameObject = sphereGO;
            Undo.RegisterCreatedObjectUndo(sphereGO, "Create Sphere");
        }

        [MenuItem("GameObject/3D Object/Cube - Animation", false, 0)]
        static void CreateCube(MenuCommand menuCommand)
        {
            GameObject sphereGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sphereGO.name = "Animation Cube";
            sphereGO.transform.SetParent(Selection.activeGameObject?.transform);
            sphereGO.transform.localPosition = Vector3.zero;
            sphereGO.transform.localRotation = Quaternion.identity;
            sphereGO.transform.localScale = new Vector3(50f, 50f, 50f);
            DestroyImmediate(sphereGO.GetComponent<BoxCollider>());
            Selection.activeGameObject = sphereGO;
            Undo.RegisterCreatedObjectUndo(sphereGO, "Create Cube");
        }
    }
}
#endif