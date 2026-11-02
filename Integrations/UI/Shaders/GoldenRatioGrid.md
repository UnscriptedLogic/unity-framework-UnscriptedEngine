# Golden Ratio Grid

Use **Tools > UnscriptedEngine > UI > GoldenRatioGrid** to create a transparent,
non-interactive UI Image. Select a RectTransform inside a Canvas first to fill it.
With no UI selection, the tool creates a Screen Space Overlay Canvas. A selected
RectTransform outside a Canvas receives a Canvas child containing the grid.
Creation is a single Undo operation.

The shared material is in Resources/UnscriptedEngine/GoldenRatioGrid.mat.
Adjust framing color, golden-section color, screen-pixel line width, framing inset,
and the three guide visibility toggles in its Inspector. Duplicate the material
and assign the copy to an Image for independent settings.

Defaults approximate the reference: yellow framing at 27.64% / 72.36%, two
corner-to-corner diagonals, four steeper diagonals, and red golden sections at
38.20% / 61.80%. No golden spiral is drawn.

Keep Image Type set to Simple with Source Image empty: the shader uses the
Image's 0-1 UVs and does not sample sprite textures. Image tint and CanvasGroup
alpha work normally. Mask stencil and RectMask2D soft clipping are supported.

The grid uses one quad, one transparent shader pass, and no runtime component.
Width is measured in render-target pixels. Full-screen overlays add transparent
overdraw; disable the GameObject when guides are not needed. The tool adds no
EventSystem or raycaster because this overlay does not accept input.
