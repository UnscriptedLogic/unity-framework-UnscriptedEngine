using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnscriptedEngine
{
    [RequireComponent(typeof(Button))]
    public class UButtonComponent : UUIComponent, IBindable
    {
        [SerializeField] private bool useTheme = true;

        private Button button;
        private Image image;

        public Button TMPButton => button;
        public Image ButtonImage => image;

        public override void InitializeUIComponent(UCanvasController context)
        {
            base.InitializeUIComponent(context);

            button = GetComponent<Button>();
            image = GetComponent<Image>();

            if (useTheme)
            {
                image.color = theme.PrimaryPalette.main;
            }
        }

        /// <summary>
        /// Binds a method to the 'OnClick' function of the button.
        /// A shorthand to TMPButton.OnClick.AddListener();
        /// </summary>
        /// <param name="method">OnClick method</param>
        public void Bind(UnityAction method)
        {
            button.onClick.AddListener(method);
        }

        /// <summary>
        /// Binds a method to the 'OnClick' function of the button.
        /// A shorthand to TMPButton.OnClick.AddListener();
        /// </summary>
        /// <param name="method">OnClick method</param>
        public void Bind(UnityAction<string> method)
        {
            button.onClick.AddListener(() => method(ID));
        }

        /// <summary>
        /// UnBinds a method to the 'OnClick' function of the button.
        /// A shorthand to TMPButton.OnClick.RemoveListener();
        /// </summary>
        /// <param name="method">OnClick method</param>
        public void UnBind(UnityAction method)
        {
            button.onClick.RemoveListener(method);
        }

        /// <summary>
        /// UnBinds all methods on the 'OnClick' function of the button.
        /// A shorthand to TMPButton.OnClick.RemoveAllListener();
        /// </summary>
        public void UnBind()
        {
            button.onClick.RemoveAllListeners();
        }
    }
}