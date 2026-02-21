using UnityEngine;

public class ResetActionIdOnEnter : StateMachineBehaviour
{
    [SerializeField] private string actionIdParam = "ActionID";
    [SerializeField] private int resetValue = 0;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null) return;
        if (!HasIntParameter(animator, actionIdParam)) return;
        animator.SetInteger(actionIdParam, resetValue);
    }

    private static bool HasIntParameter(Animator animator, string paramName)
    {
        foreach (var p in animator.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Int)
                return true;
        }
        return false;
    }
}
