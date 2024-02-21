using UnityEngine;

namespace UnscriptedEngine
{
    public static class Raycast
    {
        public static bool FromCenterCamera(Camera camera, out RaycastHit hit, float distance = 100f)
        {
            return Physics.Raycast(camera.ScreenPointToRay(new Vector3((float)Screen.width / 2f, (float)Screen.height / 2f)), out hit, distance);
        }

        public static bool FromCenterCamera(Camera camera, LayerMask layer, out RaycastHit hit, float distance = 100f)
        {
            return Physics.Raycast(camera.ScreenPointToRay(new Vector3((float)Screen.width / 2f, (float)Screen.height / 2f)), out hit, distance, layer);
        }

        public static bool FromCenterCameraGetComp<T>(Camera camera, out T? component, float distance = 100f)
        {
            if (FromCenterCamera(camera, out var hit, distance))
            {
                return hit.collider.TryGetComponent<T>(out component);
            }

            component = default(T);
            return false;
        }

        public static bool FromCenterCameraGetComp<T>(Camera camera, LayerMask layer, out T? component, float distance = 100f)
        {
            if (FromCenterCamera(camera, layer, out var hit, distance))
            {
                return hit.collider.TryGetComponent<T>(out component);
            }

            component = default(T);
            return false;
        }

        public static bool FromMousePos3D(Vector2 mousePosition, Camera camera, out RaycastHit hit, float distance = 100f)
        {
            return Physics.Raycast(camera.ScreenPointToRay(mousePosition), out hit, distance);
        }

        public static bool FromMousePos3D(Vector2 mousePosition, Camera camera, out RaycastHit hit, LayerMask layer, float distance = 100f)
        {
            return Physics.Raycast(camera.ScreenPointToRay(mousePosition), out hit, distance, layer);
        }
    } 
}