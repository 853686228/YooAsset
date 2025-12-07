using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Samples.Timeline.TestCode.LightMixerTrack
{
    [System.Serializable]
    public class LightControlClip : PlayableAsset, ITimelineClipAsset
    {
        public Color color = Color.white;
        public float intensity = 1f;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<LightControlBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.color = color;
            behaviour.intensity = intensity;
            
            return playable;
        }

        public ClipCaps clipCaps => ClipCaps.Blending;
    }
}