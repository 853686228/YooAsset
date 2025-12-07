using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Samples.Timeline.TestCode.LightMixerTrack
{
    [TrackColor(0.8f, 0.8f, 0.8f)]
    [TrackClipType(typeof(LightControlClip))]
    [TrackBindingType(typeof(Light))]
    public class LightControlTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<LightControlMixerBahaviour>.Create(graph, inputCount);
            return mixer;
        }
    }
}