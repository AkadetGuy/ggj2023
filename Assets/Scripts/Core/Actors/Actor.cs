﻿using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using pdxpartyparrot.Core.Actors.Components;
using pdxpartyparrot.Core.Util;
using pdxpartyparrot.Core.World;

using UnityEngine;
using UnityEngine.Assertions;
using Unity.VisualScripting;

namespace pdxpartyparrot.Core.Actors
{
    [RequireComponent(typeof(ScriptMachine))]
    public abstract class Actor : MonoBehaviour
    {
        [SerializeField]
        [ReadOnly]
        private Guid _id;

        public Guid Id => _id;

        public abstract float Height { get; }

        public abstract float Radius { get; }

        #region Model

        [SerializeField]
        [CanBeNull]
        private GameObject _model;

        [CanBeNull]
        public GameObject Model
        {
            get => _model;
            protected set => _model = value;
        }

        #endregion

        [Space(10)]

        #region Components

        [Header("Components")]

        [SerializeField]
        private ActorComponent[] _components;

        #region Behavior

        [Header("Behavior")]

        [SerializeField]
        [ReadOnly]
        [CanBeNull]
        private ActorBehaviorComponent _behavior;

        [CanBeNull]
        public ActorBehaviorComponent Behavior => _behavior;

        #endregion

        #region Movement

        [Header("Movement")]

        [SerializeField]
        [ReadOnly]
        [CanBeNull]
        private ActorMovementComponent _movement;

        [CanBeNull]
        public ActorMovementComponent Movement => _movement;

        [SerializeField]
        [ReadOnly]
        private bool _isMoving;

        public bool IsMoving
        {
            get => _isMoving;
            set
            {
                bool changed = IsMoving != value;
                _isMoving = value;
                if(changed) {
                    MoveStateChanged();
                }
            }
        }

        [SerializeField]
        [ReadOnly]
        private Vector3 _facingDirection = new Vector3(1.0f, 0.0f, 0.0f);

        public Vector3 FacingDirection
        {
            get => _facingDirection;
            private set => _facingDirection = value;
        }

        #endregion

        #region Animation

        [Header("Animation")]

        [SerializeField]
        [ReadOnly]
        [CanBeNull]
        private ActorManualAnimatorComponent _manualAnimator;

        [CanBeNull]
        public ActorManualAnimatorComponent ManualAnimator => _manualAnimator;

        #endregion

        #endregion

        #region Network

        public abstract bool IsLocalActor { get; }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
        }

        protected virtual void OnDestroy()
        {
            if(ActorManager.HasInstance) {
                ActorManager.Instance.Unregister(this);
            }
        }

        #endregion

        public virtual void Initialize(Guid id)
        {
            Assert.IsTrue(_id == Guid.Empty, $"Stomping existing actor id {_id} with {id}");

            if(ActorManager.Instance.EnableDebug) {
                Debug.Log($"Initializing actor {id}");
            }

            _id = id;
            name = Id.ToString();

            foreach(ActorComponent component in _components) {
                component.Initialize(this);

                // cache some useful components while we're here
                switch(component) {
                case ActorBehaviorComponent behavior:
                    Assert.IsNull(_behavior);
                    _behavior = behavior;
                    break;
                case ActorMovementComponent movement:
                    Assert.IsNull(_movement);
                    _movement = movement;
                    break;
                }
            }
        }

        public void TriggerScriptEvent(string name, params object[] args)
        {
            CustomEvent.Trigger(gameObject, name, args);
        }

        #region Components

        public bool HasActorComponent<T>() where T : ActorComponent
        {
            return null != GetActorComponent<T>();
        }

        [CanBeNull]
        public T GetActorComponent<T>() where T : ActorComponent
        {
            foreach(ActorComponent component in _components) {
                T tc = component as T;
                if(tc != null) {
                    return tc;
                }
            }
            return null;
        }

        public void GetActorComponents<T>(ICollection<T> components) where T : ActorComponent
        {
            components.Clear();
            foreach(ActorComponent component in _components) {
                T tc = component as T;
                if(tc != null) {
                    components.Add(tc);
                }
            }
        }

        public void RunOnComponents(Func<ActorComponent, bool> f)
        {
            foreach(ActorComponent component in _components) {
                if(f(component)) {
                    return;
                }
            }
        }

        #endregion

        public void Think(float dt)
        {
            RunOnComponents(c => c.OnThink(dt));
        }

        public virtual void SetFacing(Vector3 direction)
        {
            if(Mathf.Approximately(direction.sqrMagnitude, 0.0f)) {
                return;
            }

            FacingDirection = direction.normalized;

            RunOnComponents(c => c.OnSetFacing(FacingDirection));
        }

        // notifies the actor of an external movement state change
        // (useful to force the idle / moving trigger)
        public void MoveStateChanged()
        {
            OnMoveStateChanged();
        }

        // TODO: when character behavior components merge over to actor components
        // then all of the "can move" decisions should move into this
        /*public bool CanMove()
        {
            return CanMove(true);
        }

        public bool CanMove(bool components)
        {
            if(PartyParrotManager.Instance.IsPaused) {
                return false;
            }

            if(components) {
                foreach(ActorComponent component in _components) {
                    if(!component.CanMove) {
                        return false;
                    }
                }
            }

            return true;
        }*/

        // TODO: would be better if we did radius (x) and height (y) separately
        public bool Collides(Actor other, float distance = float.Epsilon)
        {
            // TODO: actors should cache their transform to use here
            Vector3 opos = null != other.Movement ? other.Movement.Position : other.transform.position;
            return Collides(opos, other.Radius, distance);
        }

        // TODO: would be better if we did radius (x) and height (y) separately
        public bool Collides(Vector3 opos, float radius, float distance = float.Epsilon)
        {
            // TODO: actors should cache their transform to use here
            Vector3 pos = null != Movement ? Movement.Position : transform.position;
            Vector3 offset = opos - pos;

            float r = radius + Radius;
            float d = r * r;
            return offset.sqrMagnitude < d;
        }

        // NOTE: it's usually necessary to run an effect trigger
        // before destroying (for animations, etc)
        // passing true here straight up destroys the object
        // and my cancel any running effects
        public void DeSpawn(bool destroy)
        {
            OnDeSpawn();

            if(destroy) {
                Debug.LogWarning($"Destroying actor {Id}");

                Destroy(gameObject);
            } else {
                gameObject.SetActive(false);
            }
        }

        #region Events

        public virtual bool OnSpawn([CanBeNull] SpawnPoint spawnpoint)
        {
            ActorManager.Instance.Register(this);

            RunOnComponents(c => c.OnSpawn(spawnpoint));

            return true;
        }

        public virtual bool OnReSpawn([CanBeNull] SpawnPoint spawnpoint)
        {
            ActorManager.Instance.Register(this);

            RunOnComponents(c => c.OnReSpawn(spawnpoint));

            return true;
        }

        public virtual void OnDeSpawn()
        {
            RunOnComponents(c => c.OnDeSpawn());

            if(ActorManager.HasInstance) {
                ActorManager.Instance.Unregister(this);
            }
        }

        protected virtual void OnMoveStateChanged()
        {
            RunOnComponents(c => c.OnMoveStateChanged());
        }

        #endregion
    }
}
