using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class EnableObjectByAiming : MonoBehaviour
{
    public XRRayInteractor leftRayInteractor;
    public XRRayInteractor rightRayInteractor;
    public float activationDistance = 5f;

    void Update()
    {
        CheckRaycast(leftRayInteractor);
        CheckRaycast(rightRayInteractor);
    }

    private void CheckRaycast(XRRayInteractor rayInteractor)
    {
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Перевірка, чи промінь спрямований на цей об'єкт
            if (hit.transform == transform)
            {
                float distance = Vector3.Distance(rayInteractor.transform.position, transform.position);

                // Перевірка відстані до об'єкта
                if (distance <= activationDistance)
                {
                    // Деактивуємо об'єкт
                    gameObject.SetActive(false);
                }
            }
        }
    }
}
