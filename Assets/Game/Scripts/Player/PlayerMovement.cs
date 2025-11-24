using System;
using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _walkSpeed;
    [SerializeField] private float _sprintSpeed;
    [SerializeField] private float _crouchSpeed;
    [SerializeField] private float _walkSprintTransition;
    [SerializeField] private float _jumpForce;
    [SerializeField] private InputManager _input;
    [SerializeField] private float _rotationSmoothTime = 0.1f;
    [SerializeField] private Transform _groundDetector;
    [SerializeField] private float _detectorRadius;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Vector3 _upperStepOffset;
    [SerializeField] private float _stepCheckerDistance;
    [SerializeField] private float _stepForce;
    [SerializeField] private Transform _climbDetector;
    [SerializeField] private float _climbCheckDistance;
    [SerializeField] private LayerMask _climbableLayer;
    [SerializeField] private Vector3 _climbOffset;
    [SerializeField] private float _climbSpeed;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CameraManager _cameraManager;
    [SerializeField] private float _glideSpeed;
    [SerializeField] private float _airDrag;
    [SerializeField] private Vector3 _glideRotationSpeed;
    [SerializeField] private float _minGlideRotationX;
    [SerializeField] private float _maxGlideRotationX;
    [SerializeField] private float _resetComboInterval;
    [SerializeField] private Transform _hitDetector;
    [SerializeField] private float _hitDetectorRadius;
    [SerializeField] private LayerMask _hitLayer;

    private Rigidbody _rigidbody;
    private float _speed;
    private float _rotationSmoothVelocity;
    private bool _isGrounded;
    private PlayerStance _playerStance;
    private Animator _animator;
    private CapsuleCollider _collider;
    private bool _isPunching;
    private int _combo = 0;
    private Coroutine _resetCombo;

    private void Awake() 
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider>();
        _speed = _walkSpeed;
        _playerStance = PlayerStance.Stand;
        HideAndLockCursor(); 
    }

    // Start is called before the first frame update
    private void Start()
    {
        // (subscribe) add method to event every time the game is started
        _input.OnMoveInput += Move;
        _input.OnSprintInput += Sprint;
        _input.OnJumpInput += Jump;
        _input.OnClimbInput += StartClimb;
        _input.OnCancelClimb += CancelClimb;
        _input.OnCrouchInput += Crouch;
        _input.OnGlideInput += StartGlide;
        _input.OnCancelGlide += CancelGlide;
        _input.OnPunchInput += Punch;
        _cameraManager.OnChangePerspective += ChangePerspective;
    }

    // Update is called once per frame
    void Update()
    {
        CheckIsGrounded();
        CheckStep();
        Glide();
    }

    private void OnDestroy()
    {
        // (unsubscribe) delete method from event every time the object/game is destroyed
        _input.OnMoveInput -= Move;
        _input.OnSprintInput -= Sprint;
        _input.OnJumpInput -= Jump;
        _input.OnClimbInput -= StartClimb;
        _input.OnCancelClimb -= CancelClimb;
        _input.OnCrouchInput -= Crouch;
        _input.OnGlideInput -= StartGlide;
        _input.OnCancelGlide -= CancelGlide;
        _input.OnPunchInput -= Punch;
        _cameraManager.OnChangePerspective -= ChangePerspective;
    }

    private void HideAndLockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Move(Vector2 axisDirection)
    {
        Vector3 movementDirection = Vector3.zero;
        bool isPlayerStanding = _playerStance == PlayerStance.Stand;
        bool isPlayerClimbing = _playerStance == PlayerStance.Climb;
        bool isPlayerCrouch = _playerStance == PlayerStance.Crouch;
        bool isPlayerGliding = _playerStance == PlayerStance.Glide;
        
        if((isPlayerStanding || isPlayerCrouch) && !_isPunching)
        {
            switch(_cameraManager.CameraState)
            {
                case CameraState.ThirdPerson:
                    if (axisDirection.magnitude >= 0.1)
                    {
                        // object rotation degree (radians to degrees)
                        float rotationAngle = Mathf.Atan2(axisDirection.x, axisDirection.y) * Mathf.Rad2Deg
                                            + _cameraTransform.eulerAngles.y;
                        float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationAngle,
                                            ref _rotationSmoothVelocity, _rotationSmoothTime);
                        // *rotation value isn't vector, but euler (degrees of x,y,z)
                        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

                        // object movement
                        // vector 2D (horizontal left/right x, vertical front/back y)
                        // vector 3D (horizontal left/right x, vertical up/down y, horizontal front/back z)
                        movementDirection = Quaternion.Euler(0f, rotationAngle, 0f) * Vector3.forward;

                        // adding force to the object player movement
                        _rigidbody.AddForce(movementDirection * _speed * Time.deltaTime);
                        // Time.deltaTime makes movement run at a consistent speed regardless of frame rate
                    }
                    break;

                case CameraState.FirstPerson:
                    transform.rotation = Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f);
                    Vector3 verticalDirection = axisDirection.y * transform.forward;
                    Vector3 horizontalDirection = axisDirection.x * transform.right;
                    movementDirection = verticalDirection + horizontalDirection;
                    _rigidbody.AddForce(movementDirection * _speed * Time.deltaTime); 
                    break;
                    
                default:
                    break;
            }

            // setting animator parameter value based on player speed * movement direction
            Vector3 velocity = new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
            _animator.SetFloat("Velocity", velocity.magnitude * axisDirection.magnitude);
            _animator.SetFloat("VelocityX", velocity.magnitude * axisDirection.x);
            _animator.SetFloat("VelocityZ", velocity.magnitude * axisDirection.y);
        }
        else if(isPlayerClimbing)
        {
            Vector3 horizontal = axisDirection.x * transform.right;
            Vector3 vertical = axisDirection.y * transform.up;
            movementDirection = horizontal + vertical;
            // _rigidbody.AddForce(movementDirection * _speed * Time.deltaTime);
            _rigidbody.AddForce(movementDirection * Time.deltaTime * _climbSpeed);
            Vector3 velocity = new Vector3(_rigidbody.velocity.x, _rigidbody.velocity.y, 0);
            _animator.SetFloat("ClimbVelocityX", velocity.magnitude * axisDirection.x);
            _animator.SetFloat("ClimbVelocityY", velocity.magnitude * axisDirection.y);
        }
        else if(isPlayerGliding)
        {
            Vector3 rotationDegree = transform.rotation.eulerAngles;
            rotationDegree.x += _glideRotationSpeed.x * axisDirection.y * Time.deltaTime;
            rotationDegree.x = Mathf.Clamp(rotationDegree.x, _minGlideRotationX, _maxGlideRotationX);
            rotationDegree.z += _glideRotationSpeed.z * axisDirection.x * Time.deltaTime;
            rotationDegree.y += _glideRotationSpeed.y * axisDirection.x * Time.deltaTime;
            transform.rotation = Quaternion.Euler(rotationDegree);
        }
    }

    private void Sprint(bool isSprint)
    {
        if(isSprint)
        {
            if(_speed < _sprintSpeed)
            {
                _speed = _speed + _walkSprintTransition * Time.deltaTime;
            }
        }
        else
        {
            if(_speed > _walkSpeed)
            {
                _speed = _speed - _walkSprintTransition * Time.deltaTime;
            }
        }
    }

    private void Jump()
    {
        if(_isGrounded)
        {
            Vector3 jumpDirection = Vector3.up;
            _rigidbody.AddForce(jumpDirection * _jumpForce * Time.deltaTime);
            _animator.SetTrigger("Jump");
        }
    }

    private void CheckIsGrounded()
    {
        // CheckSphere will return true/false
        _isGrounded = Physics.CheckSphere(_groundDetector.position, _detectorRadius, _groundLayer);
        _animator.SetBool("IsGrounded", _isGrounded);

        if(_isGrounded)
        {
            CancelGlide();
        }
    }

    private void CheckStep()
    {
        // Raycast will return true/false
        bool isHitLowerStep = Physics.Raycast(_groundDetector.position, transform.forward, 
                                                _stepCheckerDistance);
        bool isHitUpperStep = Physics.Raycast(_groundDetector.position + _upperStepOffset, 
                                                transform.forward, _stepCheckerDistance);

        if(isHitLowerStep && !isHitUpperStep)
        {
            _rigidbody.AddForce(0, _stepForce * Time.deltaTime, 0);
        }
    }

    private void StartClimb()
    {
        bool isInFrontOfClimbingWall = Physics.Raycast(_climbDetector.position, 
                                        transform.forward, out RaycastHit hit, 
                                        _climbCheckDistance, _climbableLayer);
        bool isNotClimbing = _playerStance != PlayerStance.Climb;

        if(isInFrontOfClimbingWall && _isGrounded && isNotClimbing)
        {
            // recenter the collider position when climbing
            _collider.center = Vector3.up * 1.3f;
            Vector3 offset = (transform.forward * _climbOffset.z) + (Vector3.up * _climbOffset.y);
            transform.position = hit.point - offset;
            _playerStance = PlayerStance.Climb;
            _rigidbody.useGravity = false;
            _speed = _climbSpeed;
            _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
            _cameraManager.SetTPSFielOfView(70);
            _animator.SetBool("IsClimbing", true);
        }
    }

    private void CancelClimb()
    {
        if(_playerStance == PlayerStance.Climb)
        {
            // recenter the collider position when not climbing
            _collider.center = Vector3.up * 0.9f;
            _playerStance = PlayerStance.Stand;
            _rigidbody.useGravity = true;
            // agar objek player tidak tersangkut di tembok
            transform.position -= transform.forward;
            _speed = _walkSpeed;
            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
            _cameraManager.SetTPSFielOfView(40);
            _animator.SetBool("IsClimbing", false);
        }
    }

    private void ChangePerspective()
    {
        _animator.SetTrigger("ChangePerspective");
    }

    private void Crouch()
    {
        if(_playerStance == PlayerStance.Stand)
        {
            _playerStance = PlayerStance.Crouch;
            _animator.SetBool("IsCrouch", true);
            _speed = _crouchSpeed;
            // adjust collider height when crouch
            _collider.height = 1.3f;
            _collider.center = Vector3.up * 0.66f;
        }
        else if(_playerStance == PlayerStance.Crouch)
        {
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("IsCrouch", false);
            _speed = _walkSpeed;
            _collider.height = 1.8f;
            _collider.center = Vector3.up * 0.9f;
        }
    }

    private void Glide()
    {
        if(_playerStance == PlayerStance.Glide)
        {
            Vector3 playerRotation = transform.rotation.eulerAngles;
            float lift = playerRotation.x;
            Vector3 upForce = transform.up * (lift + _airDrag);
            Vector3 forwardForce = transform.forward * _glideSpeed;
            Vector3 totalForce = upForce + forwardForce;
            _rigidbody.AddForce(totalForce * Time.deltaTime);
        }
    }

    private void StartGlide()
    {
        if(_playerStance != PlayerStance.Glide && !_isGrounded)
        {
            _playerStance = PlayerStance.Glide;
            _animator.SetBool("IsGliding", true);
            _cameraManager.SetFPSClampedCamera(true, transform.rotation.eulerAngles);
        }
    }

    private void CancelGlide()
    {
        if(_playerStance == PlayerStance.Glide)
        {
            _playerStance = PlayerStance.Stand;
            _animator.SetBool("IsGliding", false);
            _cameraManager.SetFPSClampedCamera(false, transform.rotation.eulerAngles);
        }
    }

    private void Punch()
    {
        if(!_isPunching && _playerStance == PlayerStance.Stand)
        {
            _isPunching = true;

            if(_combo < 3)
            {
                _combo = _combo + 1;
            }
            else
            {
                _combo = 1;
            }
            _animator.SetInteger("Combo", _combo);
            _animator.SetTrigger("Punch");
        }
    }

    private void EndPunch()
    {
        _isPunching = false;

        if(_resetCombo != null)
        {
            StopCoroutine(_resetCombo);
        }
        _resetCombo = StartCoroutine(ResetCombo());
    }

    private IEnumerator ResetCombo()
    {
        yield return new WaitForSeconds(_resetComboInterval);
        _combo = 0;
    }

    private void Hit()
    {
        // return array collider values
        Collider[] hitObjects = Physics.OverlapSphere(_hitDetector.position, 
                                _hitDetectorRadius, _hitLayer);
        
        for(int i = 0; i < hitObjects.Length; i++)
        {
            if(hitObjects[i].gameObject != null)
            {
                Destroy(hitObjects[i].gameObject);
            }
        }
    }
}
