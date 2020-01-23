namespace Nekoyume.Game.Character
{
    public class NPCSpineController : SpineController
    {
        protected override bool IsLoopAnimation(string animationName)
        {
            return animationName == nameof(NPCAnimation.Type.Idle_01)
                   || animationName == nameof(NPCAnimation.Type.Idle_02)
                   || animationName == nameof(NPCAnimation.Type.Idle_03)
                   || animationName == nameof(NPCAnimation.Type.Loop_01)
                   || animationName == nameof(NPCAnimation.Type.Loop_02)
                   || animationName == nameof(NPCAnimation.Type.Loop_03);
        }
    }
}