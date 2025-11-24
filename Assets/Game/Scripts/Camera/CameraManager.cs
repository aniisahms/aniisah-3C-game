using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraManager : MonoBehaviour
{
    public Action OnChangePerspective; // event for player movement animation based on POV
    [SerializeField] public CameraState CameraState;
    [SerializeField] private CinemachineVirtualCamera _fpsCamera;
    [SerializeField] private CinemachineFreeLook _tpsCamera;
    [SerializeField] private InputManager _inputManager;
    
    // Start is called before the first frame update
    private void Start()
    {
        _inputManager.OnChangePOV += SwitchCamera;
    }

    // // Update is called once per frame
    // private void Update()
    // {
        
    // }

    private void OnDestroy() 
    {
        _inputManager.OnChangePOV -= SwitchCamera;
    }

    public void SetTPSFielOfView(float fieldOfView)
    {
        _tpsCamera.m_Lens.FieldOfView = fieldOfView;
    }

    public void SetFPSClampedCamera(bool isClamped, Vector3 playerRotation)
    {
        CinemachinePOV pov = _fpsCamera.GetCinemachineComponent<CinemachinePOV>();

        if(isClamped)
        {
            // limiting the camera rotation
            pov.m_HorizontalAxis.m_Wrap = false;
            pov.m_HorizontalAxis.m_MinValue = playerRotation.y - 60;
            pov.m_HorizontalAxis.m_MaxValue = playerRotation.y + 60;
        }
        else
        {
            pov.m_HorizontalAxis.m_MinValue = -180;
            pov.m_HorizontalAxis.m_MaxValue = 180;
            pov.m_HorizontalAxis.m_Wrap = true;
        }
    }

    private void SwitchCamera()
    {
        OnChangePerspective();
        if(CameraState == CameraState.ThirdPerson)
        {
            CameraState = CameraState.FirstPerson;
            _tpsCamera.gameObject.SetActive(false);
            _fpsCamera.gameObject.SetActive(true);
        }
        else
        {
            CameraState = CameraState.ThirdPerson;
            _tpsCamera.gameObject.SetActive(true);
            _fpsCamera.gameObject.SetActive(false);
        }
    }
}
