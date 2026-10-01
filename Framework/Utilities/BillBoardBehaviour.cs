using UnityEngine;

public class BillBoardBehaviour : MonoBehaviour
{
    [System.Flags]
    public enum RotationAxis
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2
    }

    public enum BillboardAxis
    {
        PositiveX,
        NegativeX,
        PositiveY,
        NegativeY,
        PositiveZ,
        NegativeZ
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useMainCamera = true;
    [SerializeField] private BillboardAxis forwardAxis = BillboardAxis.PositiveZ;
    [SerializeField] private BillboardAxis upAxis = BillboardAxis.PositiveY;
    [SerializeField] private RotationAxis ignoreRotationAxis;

    public Camera BillboardCamera => GetCameraToFace();

    private void LateUpdate()
    {
        Camera cameraToFace = GetCameraToFace();
        if (cameraToFace == null)
        {
            return;
        }

        FaceCamera(cameraToFace);
    }

    private Camera GetCameraToFace()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (!useMainCamera)
        {
            return null;
        }

        targetCamera = Camera.main;
        return targetCamera;
    }

    private void FaceCamera(Camera cameraToFace)
    {
        Vector3 directionToCamera = cameraToFace.transform.position - transform.position;
        if (directionToCamera.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 localForward = GetAxisVector(forwardAxis);
        Vector3 localUp = GetAxisVector(upAxis);
        if (Mathf.Abs(Vector3.Dot(localForward, localUp)) > 0.999f)
        {
            return;
        }

        Quaternion targetBasis = Quaternion.LookRotation(directionToCamera.normalized, cameraToFace.transform.up);
        Quaternion localBasis = Quaternion.LookRotation(localForward, localUp);
        Quaternion targetRotation = targetBasis * Quaternion.Inverse(localBasis);

        if (ignoreRotationAxis != RotationAxis.None)
        {
            Vector3 currentEuler = transform.rotation.eulerAngles;
            Vector3 targetEuler = targetRotation.eulerAngles;

            if ((ignoreRotationAxis & RotationAxis.X) != 0)
            {
                targetEuler.x = currentEuler.x;
            }

            if ((ignoreRotationAxis & RotationAxis.Y) != 0)
            {
                targetEuler.y = currentEuler.y;
            }

            if ((ignoreRotationAxis & RotationAxis.Z) != 0)
            {
                targetEuler.z = currentEuler.z;
            }

            targetRotation = Quaternion.Euler(targetEuler);
        }

        transform.rotation = targetRotation;
    }

    private static Vector3 GetAxisVector(BillboardAxis axis)
    {
        switch (axis)
        {
            case BillboardAxis.PositiveX:
                return Vector3.right;
            case BillboardAxis.NegativeX:
                return Vector3.left;
            case BillboardAxis.PositiveY:
                return Vector3.up;
            case BillboardAxis.NegativeY:
                return Vector3.down;
            case BillboardAxis.PositiveZ:
                return Vector3.forward;
            case BillboardAxis.NegativeZ:
                return Vector3.back;
            default:
                return Vector3.forward;
        }
    }
}
