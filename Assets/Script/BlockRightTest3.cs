using UnityEngine;

public class BlockRightTest3 : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string blockRightStateName = "blockright";
    [SerializeField] private string blockLeftStateName = "blockleft";
    [SerializeField] private KeyCode rightKey = KeyCode.Space;
    [SerializeField] private KeyCode leftKey = KeyCode.Return;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(rightKey))
        {
            TriggerBlock(blockRightStateName);
            return;
        }
        if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TriggerBlock(blockLeftStateName);
        }
    }

    private void OnGUI()
    {
        // Fallback: catch key events even when Input.GetKeyDown isn't firing.
        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        if (e.keyCode == rightKey)
        {
            TriggerBlock(blockRightStateName);
            return;
        }
        if (e.keyCode == leftKey || e.keyCode == KeyCode.KeypadEnter)
        {
            TriggerBlock(blockLeftStateName);
        }
    }

    [ContextMenu("Test Block Right")]
    private void TestBlockRight()
    {
        TriggerBlock(blockRightStateName);
    }

    [ContextMenu("Test Block Left")]
    private void TestBlockLeft()
    {
        TriggerBlock(blockLeftStateName);
    }

    private void TriggerBlock(string stateName)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }
        if (animator == null) return;
        if (string.IsNullOrEmpty(stateName)) return;
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
    }
}
