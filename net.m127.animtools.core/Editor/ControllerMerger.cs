using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace M127
{
    [CreateAssetMenu(fileName = "ControllerMerger", menuName = "M127/Controller Merger")]
    public class ControllerMerger : ScriptableObject, IPreprocessBuildWithReport
    {
        public AnimatorController target;
        public AnimatorController[] source;
        private IList<(string, MessageType)> _errors = new List<(string, MessageType)>();
        public IEnumerable<(string, MessageType)> errors
        {
            get => _errors;
        }
        public int callbackOrder
        {
            get => 0;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            MergeControllers();
        }

        public void MergeControllers()
        {
            if (target is null || source is null) return;
            Undo.RecordObject(target, "Merge AnimatorControllers");
            target.layers = new AnimatorControllerLayer[0];
            target.parameters = new AnimatorControllerParameter[0];
            IDictionary<string, AnimatorControllerParameter> parameters = new Dictionary<string, AnimatorControllerParameter>();
            _errors.Clear();
            foreach (AnimatorController src in source)
            {
                if (src is null) continue;
                foreach (AnimatorControllerParameter p in src.parameters)
                {
                    if (parameters.TryGetValue(p.name, out AnimatorControllerParameter orig))
                    {
                        if (p.type != orig.type) _errors.Add(($"Parameter type mismatch: {p.name} was {orig.type}, now {p.type}", MessageType.Error));
                        switch (p.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                if (p.defaultBool != orig.defaultBool) _errors.Add(($"Parameter default value mismatch: {p.name} was {orig.defaultBool}, now {p.defaultBool}", MessageType.Warning));
                                break;
                            case AnimatorControllerParameterType.Int:
                                if (p.defaultInt != orig.defaultInt) _errors.Add(($"Parameter default value mismatch: {p.name} was {orig.defaultInt}, now {p.defaultInt}", MessageType.Warning));
                                break;
                            case AnimatorControllerParameterType.Float:
                                if (p.defaultFloat != orig.defaultFloat) _errors.Add(($"Parameter default value mismatch: {p.name} was {orig.defaultFloat}, now {p.defaultFloat}", MessageType.Warning));
                                break;
                        }
                    }
                    else
                    {
                        target.AddParameter(p);
                        parameters.Add(p.name, p);
                    }
                }
            }
            bool abort = false;
            foreach((string msg, MessageType type) in _errors)
            {
                switch(type)
                {
                    case MessageType.None:
                    case MessageType.Info:
                        Debug.Log(msg, this);
                        break;
                    case MessageType.Warning:
                        Debug.LogWarning(msg, this);
                        break;
                    default:
                    case MessageType.Error:
                        abort = true;
                        Debug.LogError(msg, this);
                        break;
                }
            }
            if (abort) return;
            foreach (AnimatorController src in source)
            {
                bool first = true;
                foreach(AnimatorControllerLayer layer in src.layers)
                {
                    if (first)
                    {
                        layer.defaultWeight = 1;
                        first = false;
                    }
                    target.AddLayer(layer);
                }
            }
        }
    }

    [CustomEditor(typeof(ControllerMerger))]
    [CanEditMultipleObjects]
    public class ControllerMergerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if(GUILayout.Button("Merge"))
            {
                foreach(Object o in serializedObject.targetObjects)
                {
                    (o as ControllerMerger)?.MergeControllers();
                }
            }
            if(serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Errors cannot be listed when editing multiple objects", MessageType.Info);
            }
            else
            {
                foreach((string msg, MessageType type) in (serializedObject.targetObject as ControllerMerger).errors)
                {
                    EditorGUILayout.HelpBox(msg, type);
                }
            }
        }
    }
}
