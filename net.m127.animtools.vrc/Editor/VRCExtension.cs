using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace M127
{
    [InitializeOnLoad]
    public class VRCExtension : Plugin
    {
        public static IEnumerable<AnimationClip> ParseVRCAvatarDescriptor(GameObject avatar)
        {
            if (avatar.TryGetComponent(out VRCAvatarDescriptor desc))
            {
                foreach (VRCAvatarDescriptor.CustomAnimLayer layer in desc.baseAnimationLayers)
                {
                    if (layer.isDefault) continue;
                    if (!layer.isEnabled) continue;
                    if (layer.animatorController)
                    {
                        foreach (AnimationClip clip in layer.animatorController.animationClips)
                        {
                            yield return clip;
                        }
                    }
                }
            }
        }

        static VRCExtension()
        {
            Plugins.RegisterExtension(new VRCExtension());
        }

        public override string Name => "VRC Extension: Recognizes animations in use on avatar descriptors.";

        public override ClipGetter Clips => ParseVRCAvatarDescriptor;
    }

    [InitializeOnLoad]
    public class DynBoneToPhysBone : Plugin
    {
        public static readonly Type DynBoneType = Type.GetType("DynamicBone, Assembly-CSharp", false);

        public static readonly Dictionary<Type, (Type, Func<string, string>)> bindings = new Dictionary<Type, (Type, Func<string, string>)>();

        static DynBoneToPhysBone()
        {
            if (DynBoneType != null)
            {
                bindings.Add(DynBoneType, (typeof(VRCPhysBone), Identity));
                Plugins.RegisterExtension(new DynBoneToPhysBone());
            }
        }

        public override string Name => "DynBone to PhysBone: Allows converting animation references to Dynamic Bones into references to PhysBones";
    }
}
