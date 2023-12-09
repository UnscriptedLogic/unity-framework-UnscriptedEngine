using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    public class URTSCamera : ULevelPawn
    {
        public enum Direction
        {
            Forward,
            Backward,
            Left,
            Right,
        }

        [SerializeField] protected float panSpeed = 20;
        [SerializeField] protected float smoothing = 0.5f;
        [SerializeField] protected Vector2 bounds = new Vector2(20, 20);
        [SerializeField] protected Transform anchor;

        protected Camera cam;

        public Camera ControllerCamera
        {
            get
            {
                if (cam == null)
                {
                    cam = GetComponent<Camera>();
                }

                return cam;
            }
        }

        /// <summary>
        /// Moves the camera in the direction over 1 frame while clamping it to the bounds
        /// </summary>
        /// <param name="direction">The direction to move the camera in</param>
        public virtual void MoveCamera(Direction direction)
        {
            Vector3 pos = anchor.position;

            switch (direction)
            {
                case Direction.Forward:
                    pos += anchor.forward * panSpeed * Time.deltaTime;
                    break;
                case Direction.Backward:
                    pos += -anchor.forward * panSpeed * Time.deltaTime;
                    break;
                case Direction.Left:
                    pos += -anchor.right * panSpeed * Time.deltaTime;
                    break;
                case Direction.Right:
                    pos += anchor.right * panSpeed * Time.deltaTime;
                    break;
                default:
                    Debug.Log("Something went wrong here");
                    break;
            }

            pos.x = Mathf.Clamp(pos.x, -bounds.x, bounds.x);
            pos.z = Mathf.Clamp(pos.z, -bounds.y, bounds.y);


            anchor.position = Vector3.Lerp(anchor.position, pos, smoothing);
        }
    }

}