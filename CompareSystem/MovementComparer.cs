using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class MovementComparer : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionProperty startReferenceCaptureAction;  
    public InputActionProperty startUserRecordingAction;     

    [Header("Reference Setup")]
    public Animator referenceAnimator; 
    public Transform referenceLeftHand;
    public Transform referenceRightHand;
    public Transform referenceHead;

    [Header("User Setup (XR Rig)")]
    public Transform leftController;  
    public Transform rightController; 
    public Transform head;            

    [Header("UI")]
    public TMP_Text feedbackText;

    private List<Vector3> userLeftPositions = new List<Vector3>();
    private List<Vector3> userRightPositions = new List<Vector3>();
    private List<Vector3> userHeadPositions = new List<Vector3>();

    private List<Quaternion> userLeftRotations = new List<Quaternion>();
    private List<Quaternion> userRightRotations = new List<Quaternion>();
    private List<Quaternion> userHeadRotations = new List<Quaternion>();

    private bool referenceCaptured = false;
    private bool isRecordingUser = false;

    // Parametry do filtrowania i skalowania wyniku
    float rotationWeight = 0.0005f; // jeszcze mniejszy wpływ rotacji
    float positionNoiseThreshold = 0.01f; // większy próg ignorowania minimalnych zmian pozycji
    float rotationNoiseThreshold = 2f; // większy próg ignorowania minimalnych rotacji

    float maxRange = 2f;

    void OnEnable()
    {
        startReferenceCaptureAction.action.performed += OnStartReferenceCapturePressed;
        startUserRecordingAction.action.performed += OnUserRecordingButtonPressed;
    }

    void OnDisable()
    {
        startReferenceCaptureAction.action.performed -= OnStartReferenceCapturePressed;
        startUserRecordingAction.action.performed -= OnUserRecordingButtonPressed;
    }

    private void OnStartReferenceCapturePressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("Nacisnąłeś A - odtwarzam wzorzec.");
        ShowMessage("Rozpoczynam odtwarzanie animacji wzorca...");

        referenceCaptured = false;
        StartCoroutine(CaptureReferenceAnimation());
    }

    private IEnumerator CaptureReferenceAnimation()
    {
        // Odtwórz animację wzorca (upewnij się, że stan Animatora ma taką samą nazwę)
        referenceAnimator.Play("Armature|wejscie", 0, 0f);

        // Czekamy aż animacja się zakończy (jeśli chcesz tylko odtworzyć)
        while (true)
        {
            var stateInfo = referenceAnimator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= 1f)
            {
                break;
            }
            yield return null;
        }

        // Animacja skończona - zakładamy, że wzorzec "jest zczytany"
        referenceCaptured = true;
        ShowMessage("Wzorzec gotowy! Naciśnij B aby nagrać swój ruch.");
    }

    private void OnUserRecordingButtonPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("Nacisnąłeś B.");
        if (!referenceCaptured)
        {
            Debug.LogWarning("Najpierw zczytaj wzorzec (A).");
            ShowMessage("Najpierw zczytaj wzorzec klawiszem A.");
            return;
        }

        if (!isRecordingUser)
        {
            StartUserRecording();
        }
        else
        {
            StopUserRecording();
        }
    }

    private void StartUserRecording()
    {
        userLeftPositions.Clear();
        userRightPositions.Clear();
        userHeadPositions.Clear();
        userLeftRotations.Clear();
        userRightRotations.Clear();
        userHeadRotations.Clear();

        isRecordingUser = true;
        Debug.Log("Rozpoczęto nagrywanie ruchu użytkownika.");
        ShowMessage("Nagrywanie ruchu... rusz się i ponownie naciśnij B aby zakończyć.");
    }

    private void StopUserRecording()
    {
        isRecordingUser = false;
        Debug.Log("Zakończono nagrywanie. Klatek: " + userLeftPositions.Count);
        ShowMessage("Analiza ruchu...");

        CompareUserMovement();
    }

    private void Update()
    {
        if (isRecordingUser)
        {
            userLeftPositions.Add(leftController.position);
            userRightPositions.Add(rightController.position);
            userHeadPositions.Add(head.position);

            userLeftRotations.Add(leftController.rotation);
            userRightRotations.Add(rightController.rotation);
            userHeadRotations.Add(head.rotation);
        }
    }

    private void CompareUserMovement()
    {
        int frameCount = userLeftPositions.Count;
        if (frameCount == 0)
        {
            Debug.LogWarning("Brak klatek ruchu!");
            ShowMessage("Brak nagranego ruchu do analizy.");
            return;
        }

        // Weźmy tylko pierwszą i ostatnią klatkę
        Vector3 startLeftPos = userLeftPositions[0];
        Vector3 startRightPos = userRightPositions[0];
        Vector3 startHeadPos = userHeadPositions[0];

        Quaternion startLeftRot = userLeftRotations[0];
        Quaternion startRightRot = userRightRotations[0];
        Quaternion startHeadRot = userHeadRotations[0];

        int lastIndex = frameCount - 1;
        Vector3 endLeftPos = userLeftPositions[lastIndex];
        Vector3 endRightPos = userRightPositions[lastIndex];
        Vector3 endHeadPos = userHeadPositions[lastIndex];

        Quaternion endLeftRot = userLeftRotations[lastIndex];
        Quaternion endRightRot = userRightRotations[lastIndex];
        Quaternion endHeadRot = userHeadRotations[lastIndex];

        float distLeft = Vector3.Distance(endLeftPos, startLeftPos);
        float distRight = Vector3.Distance(endRightPos, startRightPos);
        float distHead = Vector3.Distance(endHeadPos, startHeadPos);

        if (distLeft < positionNoiseThreshold) distLeft = 0;
        if (distRight < positionNoiseThreshold) distRight = 0;
        if (distHead < positionNoiseThreshold) distHead = 0;

        float rotLeft = Quaternion.Angle(startLeftRot, endLeftRot);
        float rotRight = Quaternion.Angle(startRightRot, endRightRot);
        float rotHead = Quaternion.Angle(startHeadRot, endHeadRot);

        if (rotLeft < rotationNoiseThreshold) rotLeft = 0;
        if (rotRight < rotationNoiseThreshold) rotRight = 0;
        if (rotHead < rotationNoiseThreshold) rotHead = 0;

        float totalMovement = distLeft + distRight + distHead;
        float totalRotation = (rotLeft + rotRight + rotHead) * rotationWeight;
        float total = totalMovement + totalRotation;

        Debug.Log($"totalMovement: {totalMovement}, totalRotationContribution: {totalRotation}, total: {total}");

        int finalAccuracy;
        if (total < 0.01f)
        {
            // Prawie brak ruchu
            finalAccuracy = 0;
        }
        else
        {
            float normalized = Mathf.Clamp01(total / maxRange);
            finalAccuracy = Mathf.RoundToInt(normalized * 100f);
        }

        Debug.Log($"Wynik: {finalAccuracy}%");

        string comment;
        if (finalAccuracy < 30)
        {
            comment = "Poćwicz jeszcze!";
        }
        else if (finalAccuracy < 70)
        {
            comment = "Już nieźle, ale może być lepiej.";
        }
        else
        {
            comment = "Świetnie Ci poszło!";
        }

        ShowMessage($"Twój wynik to: {finalAccuracy}% zgodności! {comment}");
    }

    private void ShowMessage(string message)
    {
        Debug.Log("[UI Message] " + message);
        if (feedbackText != null)
        {
            feedbackText.text = message;
        }
    }
}
