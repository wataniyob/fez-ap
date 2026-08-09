using System.Reflection;
using FezEngine.Components;
using FezEngine.Structure.Input;
using FezEngine.Tools;
using FezGame;
using MonoMod.RuntimeDetour;

/*
 * When using the CodeInputScrambler, map the inputs being used onto their scrambled counterparts while building up the
 * input sequence, so that you need to know the scrambled inputs to enter the code.
 */
namespace FEZAP.Archipelago
{
    public class CodeInputPatch : IFezapPatch
    {
        private IInputManager InputManager;
        private Type VolumesHostType;
        private Hook VolumesGrabInputHook;

        public void Init()
        {
            InputManager = ServiceHelper.Get<IInputManager>();
            VolumesHostType = typeof(Fez).Assembly.GetType("FezGame.Components.VolumesHost");
            VolumesGrabInputHook = new Hook(
                VolumesHostType.GetMethod("GrabInput", BindingFlags.NonPublic | BindingFlags.Instance),
                VolumesGrabInputHooked);
        }

        private bool VolumesGrabInputHooked(object self)
        {
            var inputField = VolumesHostType.GetField("Input", BindingFlags.NonPublic | BindingFlags.Instance);
            CodeInput codeInput = CodeInput.None;
            if (InputManager.Jump == FezButtonState.Pressed)
            {
                codeInput = CodeInput.Jump;
            }
            else if (InputManager.RotateRight == FezButtonState.Pressed)
            {
                codeInput = CodeInput.SpinRight;
            }
            else if (InputManager.RotateLeft == FezButtonState.Pressed)
            {
                codeInput = CodeInput.SpinLeft;
            }
            else if (InputManager.Left == FezButtonState.Pressed)
            {
                codeInput = CodeInput.Left;
            }
            else if (InputManager.Right == FezButtonState.Pressed)
            {
                codeInput = CodeInput.Right;
            }
            else if (InputManager.Up == FezButtonState.Pressed)
            {
                codeInput = CodeInput.Up;
            }
            else if (InputManager.Down == FezButtonState.Pressed)
            {
                codeInput = CodeInput.Down;
            }
            if (codeInput == CodeInput.None)
            {
                return false;
            }
            var Input = (List<CodeInput>)inputField.GetValue(self);
            codeInput = CodeInputScrambler.GetScrambledCode(codeInput);
            Input.Add(codeInput);
            if (Input.Count > 16)
            {
                Input.RemoveAt(0);
            }
            return true;
        }

        public void Dispose()
        {
            VolumesGrabInputHook.Dispose();
        }
    }
}
