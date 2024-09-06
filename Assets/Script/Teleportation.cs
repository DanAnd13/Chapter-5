using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Teleportation : MonoBehaviour
{
    public XRRayInteractor LeftRayInteractor;
    public XRRayInteractor RightRayInteractor;
    public float TeleportationSpeed = 0.5f;

    public void SmoothTeleportation()
    {
        if (LeftRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit HitFromLeftController))
        {
            StartCoroutine(SmoothTeleport(HitFromLeftController.point));
        }
        else if (RightRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit HitFromRightController))
        {
            StartCoroutine(SmoothTeleport(HitFromRightController.point));
        }
    }

    private IEnumerator SmoothTeleport(Vector3 TargetPosition)
    {
        Vector3 StartPosition = gameObject.transform.position;
        float Distance = Vector3.Distance(StartPosition, TargetPosition);
        float Duration = Distance / TeleportationSpeed;
        float ElapsedTime = 0f;

        while (ElapsedTime < Duration)
        {
            gameObject.transform.position = Vector3.Lerp(StartPosition, TargetPosition, ElapsedTime / Duration);
            ElapsedTime += Time.deltaTime;

            yield return null;
        }

        gameObject.transform.position = TargetPosition;
    }
}
