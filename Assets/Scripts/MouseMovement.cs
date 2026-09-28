using UnityEngine;
using UnityEngine.Networking;

public class MouseMovement : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 5f;
    [Tooltip("How close the cat must be before the mouse runs away")]
    public float flightDistance = 2f;
    [Tooltip("If cat overlaps the mouse within this distance, stop moving")]
    public float caughtDistance = 0.2f;

    private CatDetector catDetector;
    private bool _caught;

    private Vector2 _moveDirection;
    private Camera _mainCam;
    private Vector2 _screenBounds;

    // Optional: don't spam the console if detector missing
    private float _noDetectorWarnTimer = 0f;
    private const float NoDetectorWarnInterval = 3f;

    public void SetCatDetector(CatDetector cd)
    {
        catDetector = cd;
    }

    private void Start()
    {
        _mainCam = Camera.main;
        SetScreenBounds();
        PickNewDirection();
    }

    private void Update()
    {
        if (_caught) return;
        UpdateDirection();
        MoveMouse();
        CheckBounds();
    }

    private void UpdateDirection()
    {
        if (catDetector == null)
        {
            // no cat assigned yet — wander
            _noDetectorWarnTimer += Time.deltaTime;
            if (_noDetectorWarnTimer > NoDetectorWarnInterval)
            {
                Debug.LogWarning($"{name}: CatDetector not assigned yet. Spawner should call SetCatDetector().");
                _noDetectorWarnTimer = 0f;
            }
            return;
        }

        // No cat in view — wander
        if (!catDetector.HasCat) return;

        Vector2 catPos = catDetector.GetCatWorldPosition();
        float distance = Vector2.Distance(transform.position, catPos);

        if (distance <= caughtDistance)
        {
            // Mouse caught!
            Debug.Log("Caught!");
            
            using (UnityWebRequest www = UnityWebRequest.Post("http://homeassistant.local:8123/api/services/button/press", "{\"entity_id\":\"button.granary_smart_feeder_manual_feed\"}", "application/json"))
            {
                www.SetRequestHeader("Authorization",
                    "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJlMjc2ZmMxODE4MjU0MTlmOTk1MTliM2ZmNWI4OGZkZCIsImlhdCI6MTc2MjA3NTUwMywiZXhwIjoyMDc3NDM1NTAzfQ.CwWcWM0Jk3W6UYvmVbK0SQcKD-YXAhKq-djeMXM16fA");
                www.SendWebRequest();
                
            }
            
            
            _caught = true;
            return;
        }

        if (distance <= flightDistance)
        {
            // Run away from cursor
            _moveDirection = ((Vector2)transform.position - catPos).normalized;
        }
        // Else: keep current movement direction (wandering)
    }

    private void MoveMouse()
    {
        transform.Translate(_moveDirection * (moveSpeed * Time.deltaTime), Space.World);

        if (_moveDirection != Vector2.zero)
        {
            float targetAngle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void CheckBounds()
    {
        Vector3 pos = transform.position;

        if (pos.x < _screenBottomLeft.x || pos.x > _screenTopRight.x ||
            pos.y < _screenBottomLeft.y || pos.y > _screenTopRight.y)
        {
            PickNewDirection();

            pos.x = Mathf.Clamp(pos.x, _screenBottomLeft.x, _screenTopRight.x);
            pos.y = Mathf.Clamp(pos.y, _screenBottomLeft.y, _screenTopRight.y);
            transform.position = pos;
        }
    }

    // ---- helper fields and methods for proper bounds ----
    private Vector3 _screenTopRight;
    private Vector3 _screenBottomLeft;

    private void SetScreenBounds()
    {
        _screenTopRight = _mainCam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));
        _screenBottomLeft = _mainCam.ScreenToWorldPoint(Vector3.zero);
    }

    private void PickNewDirection()
    {
        float angle = Random.Range(0f, 360f);
        _moveDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;
    }
}