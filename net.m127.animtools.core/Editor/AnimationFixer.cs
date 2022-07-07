﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace M127
{
    public class AnimationFixer : EditorWindow
    {
        public static T Identity<T>(T t)
        {
            return t;
        }

        private static readonly Dictionary<Type, (Type, Func<string, string>)> replacements = new Dictionary<Type, (Type, Func<string, string>)>
        {
            [typeof(SkinnedMeshRenderer)] = (typeof(MeshRenderer), Identity),
            [typeof(MeshRenderer)] = (typeof(SkinnedMeshRenderer), Identity)
        };

        public delegate IEnumerable<AnimationClip> ClipGetter(GameObject rootObject);

        private static readonly ISet<ClipGetter> clipGetters = new HashSet<ClipGetter>
        {
            AnimationUtility.GetAnimationClips
        };
        private static readonly ISet<string> extensions = new HashSet<string>();

        public static void RegisterExtension(string notif, IDictionary<Type, (Type, Func<string, string>)> replacements, ClipGetter clipGetter)
        {
            if (notif != null) extensions.Add(notif);
            if (replacements != null)
            {
                foreach (KeyValuePair<Type, (Type, Func<string, string>)> entry in replacements)
                {
                    AnimationFixer.replacements[entry.Key] = entry.Value;
                }
            }
            if (clipGetter != null) clipGetters.Add(clipGetter);
        }

        [MenuItem("Tools/M127/AnimationFixer")]
        public static void MenuClick()
        {
            AnimationFixer w = GetWindow<AnimationFixer>();
            w.titleContent = new GUIContent("Animation Fixer");
            w.Show();
        }

        private static T SimpleObjectField<T>(T val, bool allowSceneObjects) where T : UnityEngine.Object
        {
            return (T)EditorGUILayout.ObjectField(val, typeof(T), allowSceneObjects);
        }

        private GameObject root;

        private Vector2 scroll, hdrscroll;

        private bool hdr;

        public void OnGUI()
        {
            hdr = EditorGUILayout.Foldout(hdr, "Integrations");
            if (hdr)
            {
                hdrscroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(false));
                foreach (string notif in extensions)
                {
                    EditorGUILayout.HelpBox(notif, MessageType.None);
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
            foreach (ClipGetter getter in clipGetters)
            {
                clips.UnionWith(getter(root));
            }
            if (clips.Count == 0)
            {
                EditorGUILayout.HelpBox("No Animations found on the root object. Are you sure this is the right object?", MessageType.Warning);
            }
            ISet<EditorCurveBinding> bindings = new HashSet<EditorCurveBinding>();
            foreach (AnimationClip clip in clips)
            {
                bindings.UnionWith(AnimationUtility.GetCurveBindings(clip));
                bindings.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
            }
            ISet<string> badPaths = new HashSet<string>();
            IDictionary<string, ISet<Type>> badTypes = new Dictionary<string, ISet<Type>>();
            foreach (EditorCurveBinding b in bindings)
            {
                UnityEngine.Object o = AnimationUtility.GetAnimatedObject(root, b);
                if (o is null)
                {
                    Transform t = root.transform;
                    foreach (string s in b.path.Split('/'))
                    {
                        bool success = false;
                        for (int i = 0; i < t.childCount; i++)
                        {
                            Transform c = t.GetChild(i);
                            if (c.name.Equals(s))
                            {
                                t = c;
                                success = true;
                                break;
                            }
                        }
                        if (!success)
                        {
                            t = null;
                            break;
                        }
                    }
                    if (t is null) badPaths.Add(b.path);
                    else if (badTypes.TryGetValue(b.path, out ISet<Type> bt))
                    {
                        bt.Add(b.type);
                    }
                    else
                    {
                        badTypes.Add(b.path, new HashSet<Type>
                        {
                            b.type
                        });
                    }
                }
            }
            if (badPaths.Count == 0 && badTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No broken bindings found", MessageType.Info);
            }
            else
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, false, true);
                foreach (string path in badPaths)
                {
                    EditorGUILayout.LabelField(path);
                    GameObject n = SimpleObjectField<GameObject>(null, true);
                    if (n) ChangePaths(clips, path, AnimationUtility.CalculateTransformPath(n.transform, root.transform));
                }
                foreach (KeyValuePair<string, ISet<Type>> entry in badTypes)
                {
                    foreach (Type t in entry.Value)
                    {
                        if (replacements.TryGetValue(t, out (Type type, Func<string,string> map) t2))
                        {
                            EditorGUILayout.SelectableLabel($"{entry.Key} : {t}");
                            if (GUILayout.Button($"To {t2}")) ChangeType(clips, entry.Key, t, t2.type, t2.map);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        public static void ChangePaths(IEnumerable<AnimationClip> clips, string oldpath, string newpath)
        {
            Undo.IncrementCurrentGroup();
            foreach (AnimationClip clip in clips)
            {
                bool hasUndo = false;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.path.Equals(oldpath))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Edit Binding Path");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.path = newpath;
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.path.Equals(oldpath))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Edit Binding Path");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.path = newpath;
                        ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        AnimationUtility.SetObjectReferenceCurve(clip, newBinding, curve);
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                    }
                }
            }
        }

        public static void ChangeType(IEnumerable<AnimationClip> clips, string path, Type oldType, Type newType, Func<string,string> fieldMap)
        {
            Undo.IncrementCurrentGroup();
            foreach (AnimationClip clip in clips)
            {
                bool hasUndo = false;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.path.Equals(path) && binding.type.Equals(oldType))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Edit Binding Type");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.type = newType;
                        newBinding.propertyName = fieldMap(binding.propertyName);
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.path.Equals(path) && binding.type.Equals(oldType))
                    {
                        if (!hasUndo)
                        {
                            hasUndo = true;
                            Undo.RecordObject(clip, "Edit Binding Type");
                        }
                        EditorCurveBinding newBinding = binding;
                        newBinding.type = newType;
                        newBinding.propertyName = fieldMap(binding.propertyName);
                        ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        AnimationUtility.SetObjectReferenceCurve(clip, newBinding, curve);
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                    }
                }
            }
        }
    }
}
