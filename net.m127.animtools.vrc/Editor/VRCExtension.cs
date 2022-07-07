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
    public class VRCExtension
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

        public static readonly Type DynBoneType = Type.GetType("DynamicBone, Assembly-CSharp", false);

        static VRCExtension()
        {
            AnimationFixer.RegisterExtension("VRC Extension: Recognizes animations in use on avatar descriptors.", null, ParseVRCAvatarDescriptor);
            if(DynBoneType != null) AnimationFixer.RegisterExtension("DynBone to PhysBone: Allows converting animation references to Dynamic Bones into references to PhysBones", new Dictionary<Type, (Type, Func<string, string>)> { [DynBoneType] = (typeof(VRCPhysBone), AnimationFixer.Identity) }, null);
        }
    }
}
