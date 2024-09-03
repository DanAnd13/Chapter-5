using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class EnableObjectByAiming : MonoBehaviour
{
    public XRRayInteractor LeftRayInteractor;
    public XRRayInteractor RightRayInteractor;
    public float ActivationDistance = 5f;

    private void Update()
    {
        CheckRaycast(LeftRayInteractor);
        CheckRaycast(RightRayInteractor);
    }

    private void CheckRaycast(XRRayInteractor rayInteractor)
    {
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                float distance = Vector3.Distance(rayInteractor.transform.position, transform.position);
                if (distance <= ActivationDistance)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
