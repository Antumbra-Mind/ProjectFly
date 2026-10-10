using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float sprintSpeed = 10.0f;
    [SerializeField] private float gravity = -19.62f; // Трохи сильніша за земну, щоб персонаж не плавав у повітрі
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Look / Camera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float minPitch = -85.0f;
    [SerializeField] private float maxPitch = 85.0f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float cameraPitch = 0.0f;

    private void Awake()
    {
        controller = gameObject.GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Ховаємо і блокуємо курсор у центрі екрана
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Поворот голови вгору-вниз (нахил камери)
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

        // Поворот усього тіла вліво-вправо навколо осі Y
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        // Скидаємо вертикальну швидкість, коли твердо стоїмо на землі
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2.0f; // Невелике притискання до підлоги для рівного спуску зі схилів
        }

        // Ввід WASD
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Рух відносно напрямку погляду персонажа
        Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;

        // Спринт на Left Shift
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Стрибок на Space
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        // Гравітація
        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }
}