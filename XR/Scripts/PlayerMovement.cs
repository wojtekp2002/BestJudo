using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public InputActionProperty moveInput; // Obsługa wejścia z joysticka
    public float speed = 3.0f; // Prędkość poruszania się
    public CharacterController characterController;

    void Update()
    {
        // Pobierz dane wejściowe z joysticka
        Vector2 input = moveInput.action.ReadValue<Vector2>();

        // Przetwórz ruch w osi X i Z
        Vector3 move = new Vector3(input.x, 0, input.y);
        move = Camera.main.transform.TransformDirection(move); // Dostosuj kierunek do kamery
        move.y = 0; // Ignoruj oś Y, aby uniknąć ruchu w górę/dół

        // Przesuń postać
        characterController.Move(move * speed * Time.deltaTime);
    }
}

