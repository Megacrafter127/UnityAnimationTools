using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace M127
{
    public class AnimationCopier : EditorWindow
    {
        [MenuItem("Tools/M127/AnimationCopier")]
        public static void MenuClick()
        {
            AnimationCopier w = GetWindow<AnimationCopier>();
            w.titleContent = new GUIContent("Animation Copier");
            w.Show();
        }

        private static T SimpleObjectField<T>(T val, bool allowSceneObjects) where T : UnityEngine.Object
        {
            return (T)EditorGUILayout.ObjectField(val, typeof(T), allowSceneObjects);
        }

        private GameObject root;

        private GameObject src;
        private GameObject dst;

        private Vector2 scroll, hdrscroll;

        private bool hdr;

        private ISet<AnimationClip> foldedClips = new HashSet<AnimationClip>();

        public void OnGUI()
        {
            hdr = EditorGUILayout.Foldout(hdr, "Integrations");
            if (hdr)
            {
                hdrscroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(false));
                foreach (Plugin plug in Plugins.plugins)
                {
                    EditorGUILayout.HelpBox(plug.Name, MessageType.None);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Separator();
            }
            root = SimpleObjectField(root, true);
            if (root is null)
            {
                EditorGUILayout.HelpBox("More Options will be displayed once a root object is provided.", MessageType.Info);
                return;
            }
            ISet<AnimationClip> clips = new HashSet<AnimationClip>();
            foreach (Plugin.ClipGetter getter in Plugins.clipGetters)
            {
                clips.UnionWith(getter(root));
            }
            foldedClips.IntersectWith(clips);
            if (clips.Count == 0)
            {
                EditorGUILayout.HelpBox("No Animations found on the root object. Are you sure this is the right object?", MessageType.Warning);
            }
            src = SimpleObjectField(src, true);
            string srcpath = AnimationUtility.CalculateTransformPath(src.transform, root.transform);
            if (src is null)
            {
                EditorGUILayout.HelpBox("More Options will be displayed once a Source object is provided.", MessageType.Info);
                return;
            }
            dst = SimpleObjectField(dst, true);
            string dstpath = AnimationUtility.CalculateTransformPath(dst.transform, root.transform);
            IDictionary<AnimationClip, ISet<EditorCurveBinding>> bindings = new Dictionary<AnimationClip, ISet<EditorCurveBinding>>();
            foreach (AnimationClip clip in clips)
            {
                ISet<EditorCurveBinding> lbindings = new HashSet<EditorCurveBinding>();
                EditorCurveBinding[] gbindings = AnimationUtility.GetCurveBindings(clip);
                foreach (EditorCurveBinding b in gbindings)
                {
                    if(srcpath.Equals(b.path))
                    {
                        lbindings.Add(b);
                    }
                }
                foreach (EditorCurveBinding b in gbindings)
                {
                    if (dstpath.Equals(b.path))
                    {
                        EditorCurveBinding b2 = b;
                        b2.path = srcpath;
                        lbindings.Remove(b2);
                    }
                }
                gbindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (EditorCurveBinding b in gbindings)
                {
                    if (srcpath.Equals(b.path))
                    {
                        lbindings.Add(b);
                    }
                }
                foreach (EditorCurveBinding b in gbindings)
                {
                    if (dstpath.Equals(b.path))
                    {
                        EditorCurveBinding b2 = b;
                        b2.path = srcpath;
                        lbindings.Remove(b2);
                    }
                }
                if(lbindings.Count != 0) bindings.Add(clip, lbindings);
            }
            if (bindings.Count == 0)
            {
                EditorGUILayout.HelpBox("No copyable bindings found", MessageType.Info);
            }
            else
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, false, true);
                foreach (KeyValuePair<AnimationClip, ISet<EditorCurveBinding>> entry in bindings)
                {
                    EditorGUILayout.LabelField(entry.Key.name);
                    if (EditorGUILayout.BeginFoldoutHeaderGroup(foldedClips.Contains(entry.Key), entry.Key.name))
                    {
                        foldedClips.Add(entry.Key);
                    }
                    else
                    {
                        foldedClips.Remove(entry.Key);
                    }
                    foreach (EditorCurveBinding b in entry.Value)
                    {
                        EditorGUILayout.LabelField(b.type.Name);
                        EditorGUILayout.LabelField(b.propertyName);
                        if (GUILayout.Button("Copy")) CopyBinding(clips, b, dstpath);
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        public static void CopyBinding(IEnumerable<AnimationClip> clips, EditorCurveBinding srcBinding, string newpath)
        {
            Undo.IncrementCurrentGroup();
            foreach (AnimationClip clip in clips)
            {
                bool hasUndo = false;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.Equals(srcBinding))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Copy Binding");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.path = newpath;
                        if (AnimationUtility.GetEditorCurve(clip, newBinding) != null)
                        {
                            if (!EditorUtility.DisplayDialog("Already in use", "Reference is already in use.\nOverwrite?", "Yes", "No")) continue;
                        }
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                    }
                }
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.Equals(srcBinding))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Copy Binding");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.path = newpath;
                        if (AnimationUtility.GetObjectReferenceCurve(clip, newBinding) != null)
                        {
                            if (!EditorUtility.DisplayDialog("Already in use", "Reference is already in use.\nOverwrite?", "Yes", "No")) continue;
                        }
                        ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        AnimationUtility.SetObjectReferenceCurve(clip, newBinding, curve);
                    }
                }
            }
        }
    }
}
