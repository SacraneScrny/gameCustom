using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Logic.Extensions;
using Logic.GameCustom.Abstracts;

using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Logic.GameCustom.SubComponents
{
    [RequireComponent(typeof(Animator))]
    public class AnimatorSubComponent : GameEntity
    {
        public const string animationPlay = "animation::play";
        public const string animationGetCurrentClip = "animation::getCurrentClip";
        public const string animationPlayWithForce = "animation::play::withForce";
        public const string animationRegisterTemporaryClips = "animation::registerTemporaryClips";
        
        public ClipElement[] Clips;
        
        private List<ClipElement> _temporaryClips = new ();
        
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationClipPlayable _currentClipPlayable;

        private uint _currentClipHash;
        private string _previousClipKey;
        private string _currentClipKey;
        private string _defaultClipKey;
        private readonly Dictionary<string, AnimationClip> _clips = new ();
        private readonly Dictionary<string, int> _clipPriorities = new ();
        
        private protected override void OnAwake()
        {
            _animator = GetComponent<Animator>();
        }
        private protected override void OnStart()
        {
            RecacheClips();
        }

        private void RecacheClips()
        {
            if (_graph.IsValid())
                _graph.Destroy();
            _graph = PlayableGraph.Create(
                ((transform.parent ?? transform).GetComponent<GameEntity>() ?? GetComponent<GameEntity>()).Guid
                + "_Controller"
            );
            
            _clips.Clear();
            _clipPriorities.Clear();
            
            foreach (var clip in Clips.Concat(_temporaryClips).Where(x => x.Clip != null))
            {
                if (!_clips.TryAdd(clip.Key, clip.Clip)) continue;
                _clipPriorities.Add(clip.Key, clip.Priority);
                if (clip.IsDefault) _defaultClipKey = clip.Key;
            }
            _defaultClipKey = string.IsNullOrEmpty(_defaultClipKey) 
                ? Clips.First(x => x.Clip != null).Key
                : _defaultClipKey;
            
            SetCurrentClip(_defaultClipKey);
            
            _output = AnimationPlayableOutput.Create(_graph, "Animation output", _animator);
            PlayClip(_defaultClipKey);
            _graph.Play();
        }
        
        private protected override void OnRegister()
        {
            RegisterMessage<string>(animationPlay, PlayMessage);
            RegisterAnswer<string>(animationGetCurrentClip, () => _currentClipKey);

            RegisterMessage<List<ClipElement>>(animationRegisterTemporaryClips, InsertTemporaryClips);
            RegisterMessage<ClipElement[]>(animationRegisterTemporaryClips, (x) => InsertTemporaryClips(x.ToList()).ToArray());
        }
        private string PlayMessage(string key)
        {
            if (_currentClipHash == key.MurmurHash()) return string.Empty;
            if (!_clips.TryGetValue(key, out var clip)) return string.Empty;
            if (!IsPriority(key)) return string.Empty;

            SetCurrentClip(key);
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
            _playCoroutine = StartCoroutine(ClipPlay(key));
            return key;
        }
        
        private Coroutine _playCoroutine;
        private IEnumerator ClipPlay(string key)
        {
            SendEvent("OnClipStart_" + key);
            SendEvent("OnClipStart");
            
            var duration = PlayClip(key);
            yield return null;

            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                yield return null;
            }
            
            SetCurrentClip(_defaultClipKey);
            PlayClip(_defaultClipKey);
            
            SendEvent("OnClipDone_" + key);
            SendEvent("OnClipDone");
        }
        private float PlayClip(string key)
        {
            if (_currentClipPlayable.IsValid())
            {
                _currentClipPlayable.Destroy();
            }

            _currentClipPlayable = AnimationClipPlayable.Create(_graph, _clips[key]);
            _output.SetSourcePlayable(_currentClipPlayable);
            
            if (!_graph.IsPlaying())
                _graph.Play();

            return _clips[key].length;
        }

        public void Func(string key) => SendEvent(key);
        
        private bool IsPriority(string clipKey) => _clipPriorities[clipKey] >= _clipPriorities[_currentClipKey];
        private void SetCurrentClip(string clipKey)
        {
            _previousClipKey = _currentClipKey;
            _currentClipKey = clipKey;
            _currentClipHash = _currentClipKey.MurmurHash();
        }

        private List<ClipElement> InsertTemporaryClips(List<ClipElement> clips)
        {
            _temporaryClips = new List<ClipElement>(clips);
            RecacheClips();
            return clips;
        }
        
        private protected override void OnEntityDestroy() => _graph.Destroy();
    }

    [System.Serializable]
    public class ClipElement
    {
        public string Key;
        public AnimationClip Clip;
        public bool IsDefault;
        public int Priority;
    }
}