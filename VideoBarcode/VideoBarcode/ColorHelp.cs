namespace VideoBarcode;

internal static class ColorHelp
{
    /*
     * RGB->HSV and HSV->RGB functions adapted from https://www.cs.rit.edu/~ncs/color/t_convert.html
     */
    internal static (float H, float S, float V) RGBtoHSV(float r, float g, float b)
    {
        float min = Math.Min(r, Math.Min(g, b));
        float max = Math.Max(r, Math.Max(g, b));

        float v = max;
        if (max == 0)
        {
            // r = g = b = 0		// s = 0, v is undefined
            return (0, 0, 0);
        }

        float delta = max - min;
        float s = delta / max;

        float h;
        if (r == max)
        {
            // between yellow & magenta
            h = (g - b) / delta;
        }
        else if (g == max)
        {
            // between cyan & yellow
            h = 2 + (b - r) / delta;
        }
        else
        {
            // between magenta & cyan
            h = 4 + (r - g) / delta;
        }

        // degrees
        h *= 60;
        if (h < 0)
        {
            h += 360;
        }

        if (float.IsNaN(h))
        {
            h = 0;
        }

        return (h, s, v);
    }

    internal static (float R, float G, float B) HSVtoRGB(float h, float s, float v)
    {
        float r, g, b;

        if (s == 0)
        {
            // achromatic (grey)
            r = g = b = v;
            return (r, g, b);
        }

        // sector 0 to 5
        h /= 60;
        int i = (int)Math.Floor(h);

        // factorial part of h
        float f = h - i;
        float p = v * (1 - s);
        float q = v * (1 - s * f);
        float t = v * (1 - s * (1 - f));

        switch (i)
        {
            case 0:
                r = v;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = v;
                b = p;
                break;
            case 2:
                r = p;
                g = v;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = v;
                break;
            case 4:
                r = t;
                g = p;
                b = v;
                break;
            default:        // case 5:
                r = v;
                g = p;
                b = q;
                break;
        }

        return (r, g, b);
    }
}
