using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class LineGraphRenderer : MonoBehaviour
{
    [Header("Texture Resolution")]
    public int texWidth  = 512;
    public int texHeight = 320;

    // Margins: left/bottom leave room for axis labels, top leaves room for legend
    private const int PAD_LEFT   = 46;
    private const int PAD_RIGHT  = 16;
    private const int PAD_TOP    = 36;  // increased for legend bar
    private const int PAD_BOTTOM = 32;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.06f, 0.08f, 0.14f, 1f);
    public Color gridColor        = new Color(1f, 1f, 1f, 0.08f);
    public Color axisColor        = new Color(1f, 1f, 1f, 0.35f);
    public Color labelColor       = new Color(1f, 1f, 1f, 0.75f);
    public Color legendBgColor    = new Color(0f, 0f, 0f, 0.45f);

    // Series colours
    // Curl/Squat: [0]=green [1]=yellow [2]=red
    // Balance:    [0]=amber(best) [1]=blue(total)
    public Color[] seriesColors = new Color[]
    {
        new Color(0.22f, 0.90f, 0.40f, 1f),   // green
        new Color(0.98f, 0.85f, 0.10f, 1f),   // yellow
        new Color(0.95f, 0.25f, 0.25f, 1f),   // red
        new Color(0.96f, 0.65f, 0.15f, 1f),   // amber
        new Color(0.25f, 0.60f, 0.98f, 1f),   // blue
    };

    public class Series
    {
        public string      name;
        public List<float> xValues;
        public List<float> yValues;
        public int         colorIndex;
    }

    public enum TimeRange    { Week, Month, Year }
    public enum ExerciseKind { RepBased, Balance }

    private RawImage  rawImage;
    private Texture2D tex;
    private Color32[] buf;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        rawImage.texture = tex;
        buf = new Color32[texWidth * texHeight];
    }

    // ===================== MAIN ENTRY POINT =====================

    public void DrawGraph(List<Series> seriesList, TimeRange range, ExerciseKind kind)
    {
        int xMin = 1;
        int xMax = (range == TimeRange.Week) ? 7 : (range == TimeRange.Month) ? 30 : 12;
        int yMin = 0;
        int yMax = (kind == ExerciseKind.Balance) ? 60 : 20;

        // 1. Fill background
        Color32 bg = Clr(backgroundColor);
        for (int i = 0; i < buf.Length; i++) buf[i] = bg;

        // 2. Grid lines
        DrawGrid(xMin, xMax, yMin, yMax, range);

        // 3. Axes
        HLine(PY(yMin, yMin, yMax), axisColor);
        VLine(PX(xMin, xMin, xMax), axisColor);

        // 4. Axis labels (bigger font)
        DrawXLabels(xMin, xMax, range);
        DrawYLabels(yMin, yMax);

        // 5. Legend bar at the top
        if (seriesList != null && seriesList.Count > 0)
            DrawLegend(seriesList);

        // 6. Series data
        bool hasData = false;
        if (seriesList != null)
        {
            foreach (var s in seriesList)
            {
                if (s == null || s.xValues == null || s.xValues.Count == 0) continue;
                hasData = true;

                Color c = (s.colorIndex >= 0 && s.colorIndex < seriesColors.Length)
                          ? seriesColors[s.colorIndex] : Color.white;

                var pts = new List<Vector2Int>();
                for (int i = 0; i < s.xValues.Count; i++)
                    pts.Add(new Vector2Int(PX(s.xValues[i], xMin, xMax), PY(s.yValues[i], yMin, yMax)));

                pts.Sort((a, b) => a.x.CompareTo(b.x));

                for (int i = 0; i < pts.Count - 1; i++)
                    Line(pts[i], pts[i + 1], c, 2);

                foreach (var p in pts)
                    Dot(p, c, 4);
            }
        }

        if (!hasData) DrawNoData();

        // 7. Push to GPU
        tex.SetPixels32(buf);
        tex.Apply();
    }

    // ===================== LEGEND =====================

    private void DrawLegend(List<Series> seriesList)
    {
        // Draw a semi-transparent background bar across the top
        int barY     = texHeight - PAD_TOP + 2;
        int barH     = PAD_TOP - 4;
        int barX     = PAD_LEFT;
        int barW     = texWidth - PAD_LEFT - PAD_RIGHT;

        // Fill legend background
        for (int x = barX; x < barX + barW; x++)
            for (int y = barY; y < barY + barH; y++)
                Pix(x, y, legendBgColor);

        // Draw each series: [swatch] [name]
        // Entries sit side by side, spaced evenly
        int entryW    = barW / seriesList.Count;
        int swatchSz  = 8;
        int textScale = 2; // 2× scaling for legend names

        for (int si = 0; si < seriesList.Count; si++)
        {
            var s = seriesList[si];
            Color c = (s.colorIndex >= 0 && s.colorIndex < seriesColors.Length)
                      ? seriesColors[s.colorIndex] : Color.white;

            int entryX = barX + si * entryW + 4;
            int centreY = barY + barH / 2;

            // Colour swatch (filled square)
            for (int sx = 0; sx < swatchSz; sx++)
                for (int sy = 0; sy < swatchSz; sy++)
                    Pix(entryX + sx, centreY - swatchSz / 2 + sy, c);

            // Label text to the right of swatch, scaled up 2×
            int textX = entryX + swatchSz + 4;
            int textY = centreY - 4;
            DrawTextScaled(s.name, textX, textY, labelColor, textScale);
        }
    }

    // ===================== COORDINATE HELPERS =====================

    private int PlotW => texWidth  - PAD_LEFT - PAD_RIGHT;
    private int PlotH => texHeight - PAD_TOP  - PAD_BOTTOM;

    private int PX(float x, int xMin, int xMax)
    {
        float n = (xMax == xMin) ? 0f : (x - xMin) / (float)(xMax - xMin);
        return Mathf.Clamp(PAD_LEFT + Mathf.RoundToInt(n * PlotW), PAD_LEFT, texWidth - PAD_RIGHT - 1);
    }

    private int PY(float y, int yMin, int yMax)
    {
        float n = (yMax == yMin) ? 0f : (y - yMin) / (float)(yMax - yMin);
        return Mathf.Clamp(PAD_BOTTOM + Mathf.RoundToInt(n * PlotH), PAD_BOTTOM, texHeight - PAD_TOP - 1);
    }

    // ===================== GRID =====================

    private void DrawGrid(int xMin, int xMax, int yMin, int yMax, TimeRange range)
    {
        int steps = 4;
        for (int i = 0; i <= steps; i++)
        {
            float yVal = yMin + (yMax - yMin) * i / (float)steps;
            HLine(PY(yVal, yMin, yMax), gridColor);
        }
        foreach (int tick in XTicks(xMin, xMax, range))
            VLine(PX(tick, xMin, xMax), gridColor);
    }

    private int[] XTicks(int xMin, int xMax, TimeRange range)
    {
        if (range == TimeRange.Week)  return new[] { 1,2,3,4,5,6,7 };
        if (range == TimeRange.Month) return new[] { 1,5,10,15,20,25,30 };
        return new[] { 1,2,3,4,5,6,7,8,9,10,11,12 };
    }

    // ===================== AXIS LABELS =====================

    private void DrawXLabels(int xMin, int xMax, TimeRange range)
    {
        string[] months = {"J","F","M","A","M","J","J","A","S","O","N","D"};
        foreach (int tick in XTicks(xMin, xMax, range))
        {
            string lbl = (range == TimeRange.Year) ? months[tick - 1] : tick.ToString();
            int px = PX(tick, xMin, xMax);
            // Centre under tick — each scaled char is 4*scale wide
            int charW  = 4 * 2; // scale=2
            int textX  = px - (lbl.Length * charW) / 2;
            int textY  = PAD_BOTTOM - 28;
            DrawTextScaled(lbl, textX, textY, labelColor, 2);
        }
    }

    private void DrawYLabels(int yMin, int yMax)
    {
        int steps = 4;
        for (int i = 0; i <= steps; i++)
        {
            int val   = yMin + (yMax - yMin) * i / steps;
            int py    = PY(val, yMin, yMax);
            string lbl = val.ToString();
            // Right-align: each scaled char 4*scale wide
            int charW  = 4 * 2;
            int textX  = PAD_LEFT - lbl.Length * charW - 4;
            int textY  = py - 4;
            DrawTextScaled(lbl, textX, textY, labelColor, 2);
        }
    }

    // ===================== SCALED PIXEL FONT =====================
    // Draws 3×5 glyphs scaled by 'scale' factor for readability

    private static readonly Dictionary<char, byte[]> G = new Dictionary<char, byte[]>
    {
        {'0',new byte[]{0b111,0b101,0b101,0b101,0b111}},
        {'1',new byte[]{0b010,0b110,0b010,0b010,0b111}},
        {'2',new byte[]{0b111,0b001,0b111,0b100,0b111}},
        {'3',new byte[]{0b111,0b001,0b111,0b001,0b111}},
        {'4',new byte[]{0b101,0b101,0b111,0b001,0b001}},
        {'5',new byte[]{0b111,0b100,0b111,0b001,0b111}},
        {'6',new byte[]{0b111,0b100,0b111,0b101,0b111}},
        {'7',new byte[]{0b111,0b001,0b001,0b001,0b001}},
        {'8',new byte[]{0b111,0b101,0b111,0b101,0b111}},
        {'9',new byte[]{0b111,0b101,0b111,0b001,0b111}},
        {'J',new byte[]{0b001,0b001,0b001,0b101,0b111}},
        {'F',new byte[]{0b111,0b100,0b110,0b100,0b100}},
        {'M',new byte[]{0b101,0b111,0b101,0b101,0b101}},
        {'A',new byte[]{0b010,0b101,0b111,0b101,0b101}},
        {'S',new byte[]{0b111,0b100,0b111,0b001,0b111}},
        {'O',new byte[]{0b010,0b101,0b101,0b101,0b010}},
        {'N',new byte[]{0b101,0b111,0b111,0b101,0b101}},
        {'D',new byte[]{0b110,0b101,0b101,0b101,0b110}},
        {'E',new byte[]{0b111,0b100,0b110,0b100,0b111}},
        {'G',new byte[]{0b111,0b100,0b101,0b101,0b111}},
        {'R',new byte[]{0b110,0b101,0b110,0b101,0b101}},
        {'T',new byte[]{0b111,0b010,0b010,0b010,0b010}},
        {'L',new byte[]{0b100,0b100,0b100,0b100,0b111}},
        {'B',new byte[]{0b110,0b101,0b110,0b101,0b110}},
        {'C',new byte[]{0b111,0b100,0b100,0b100,0b111}},
        {'H',new byte[]{0b101,0b101,0b111,0b101,0b101}},
        {'I',new byte[]{0b111,0b010,0b010,0b010,0b111}},
        {'K',new byte[]{0b101,0b110,0b100,0b110,0b101}},
        {'P',new byte[]{0b110,0b101,0b110,0b100,0b100}},
        {'U',new byte[]{0b101,0b101,0b101,0b101,0b111}},
        {'W',new byte[]{0b101,0b101,0b101,0b111,0b101}},
        {'Y',new byte[]{0b101,0b101,0b111,0b010,0b010}},
        {'Z',new byte[]{0b111,0b001,0b010,0b100,0b111}},
        {' ',new byte[]{0b000,0b000,0b000,0b000,0b000}},
    };

    // Draws text with each pixel blown up to scale×scale blocks
    private void DrawTextScaled(string text, int sx, int sy, Color c, int scale)
    {
        int cx = sx;
        foreach (char ch in text.ToUpper())
        {
            if (!G.TryGetValue(ch, out byte[] rows)) { cx += 4 * scale; continue; }
            for (int row = 0; row < rows.Length; row++)
            {
                int py = sy + (rows.Length - 1 - row) * scale;
                for (int col = 0; col < 3; col++)
                {
                    if ((rows[row] & (1 << (2 - col))) != 0)
                    {
                        // Fill scale×scale block for this pixel
                        for (int bx = 0; bx < scale; bx++)
                            for (int by = 0; by < scale; by++)
                                Pix(cx + col * scale + bx, py + by, c);
                    }
                }
            }
            cx += 4 * scale; // 3px glyph + 1px gap, scaled
        }
    }

    // ===================== PRIMITIVES =====================

    private void HLine(int py, Color c)
    {
        for (int x = PAD_LEFT; x < texWidth - PAD_RIGHT; x++) Pix(x, py, c);
    }

    private void VLine(int px, Color c)
    {
        for (int y = PAD_BOTTOM; y < texHeight - PAD_TOP; y++) Pix(px, y, c);
    }

    private void Line(Vector2Int a, Vector2Int b, Color c, int thickness)
    {
        int x0=a.x, y0=a.y, x1=b.x, y1=b.y;
        int dx=Mathf.Abs(x1-x0), sx=x0<x1?1:-1;
        int dy=-Mathf.Abs(y1-y0), sy=y0<y1?1:-1;
        int err=dx+dy, half=thickness/2;
        while(true)
        {
            for(int tx=-half;tx<=half;tx++)
                for(int ty=-half;ty<=half;ty++)
                    Pix(x0+tx, y0+ty, c);
            if(x0==x1&&y0==y1) break;
            int e2=2*err;
            if(e2>=dy){err+=dy;x0+=sx;}
            if(e2<=dx){err+=dx;y0+=sy;}
        }
    }

    private void Dot(Vector2Int center, Color c, int r)
    {
        for(int x=-r;x<=r;x++)
            for(int y=-r;y<=r;y++)
                if(x*x+y*y<=r*r) Pix(center.x+x, center.y+y, c);
    }

    private void DrawNoData()
    {
        int midY = PAD_BOTTOM + PlotH / 2;
        for (int x = PAD_LEFT; x < texWidth - PAD_RIGHT; x++)
            if ((x / 8) % 2 == 0)
                Pix(x, midY, new Color(1f, 1f, 1f, 0.12f));
        DrawTextScaled("NO DATA", PAD_LEFT + PlotW/2 - 28*2, midY + 8, labelColor, 2);
    }

    private void Pix(int x, int y, Color c)
    {
        if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return;
        int idx = y * texWidth + x;
        Color32 e = buf[idx];
        float a = c.a;
        buf[idx] = new Color32(
            (byte)(e.r*(1-a) + c.r*255*a),
            (byte)(e.g*(1-a) + c.g*255*a),
            (byte)(e.b*(1-a) + c.b*255*a),
            255);
    }

    private static Color32 Clr(Color c) =>
        new Color32((byte)(c.r*255),(byte)(c.g*255),(byte)(c.b*255),(byte)(c.a*255));

    void OnDestroy() { if (tex != null) Destroy(tex); }
}