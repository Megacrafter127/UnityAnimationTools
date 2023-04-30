using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace M127
{
    public abstract class Plugin
    {
        public delegate IEnumerable<AnimationClip> ClipGetter(GameObject rootObject);

        public abstract string Name {
            get;
        }

        public virtual ClipGetter Clips {
            get => null;
        }

        public static T Identity<T>(T t)
        {
            return t;
        }

        private static readonly Dictionary<Type, (Type, Func<string, string>)> EmptyDict = new Dictionary<Type, (Type, Func<string, string>)>();

        public virtual IReadOnlyDictionary<Type, (Type, Func<string, string>)> BindingReplacements {
            get => EmptyDict;
        }
    }
    public class Plugins
    {
        public readonly static ISet<Plugin.ClipGetter> clipGetters = new HashSet<Plugin.ClipGetter> {
            AnimationUtility.GetAnimationClips
        };

        public readonly static Dictionary<Type, (Type, Func<string, string>)> bindingReplacements = new Dictionary<Type, (Type, Func<string, string>)> {
            [typeof(SkinnedMeshRenderer)] = (typeof(MeshRenderer), Plugin.Identity),
            [typeof(MeshRenderer)] = (typeof(SkinnedMeshRenderer), Plugin.Identity)
        };

        public readonly static IList<Plugin> plugins = new List<Plugin>();

        public static void RegisterExtension(Plugin plugin)
        {
            plugins.Add(plugin);
            if (!(plugin.Clips is null)) clipGetters.Add(plugin.Clips);
            foreach(KeyValuePair<Type, (Type, Func<string, string>)> entry in plugin.BindingReplacements)
            {
                bindingReplacements.Add(entry.Key, entry.Value);
            }
        }
    }

}
