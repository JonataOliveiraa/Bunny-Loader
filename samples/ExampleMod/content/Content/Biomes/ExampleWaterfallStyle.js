// A cachoeira da água de exemplo, como o ExampleWaterfallStyle do tModLoader:
// a textura ao lado deste arquivo, e ela ilumina.
export class ExampleWaterfallStyle extends ModWaterfallStyle {
    AddLight(i, j) {
        Terraria.Lighting['void AddLight(int i, int j, float r, float g, float b)'](i, j, 0.5, 0.5, 0.5);
    }
}
