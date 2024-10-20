namespace VideoBarcode;

public static class ColorHelp
{
    /*
     * RGB->HSV and HSV->RGB functions adapted from https://www.cs.rit.edu/~ncs/color/t_convert.html
     */

    public static void RGBtoHSV(float r, float g, float b, out float h, out float s, out float v)
    {
        float min, max, delta;
        min = Math.Min(r, Math.Min(g, b));
        max = Math.Max(r, Math.Max(g, b));

        v = max;
        delta = max - min;

        if (max != 0)
        {
            s = delta / max;
        }
        else
        {
            // r = g = b = 0		// s = 0, v is undefined
            s = 0;
            h = 0;
            return;
        }

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
    }

    public static void HSVtoRGB(out float r, out float g, out float b, float h, float s, float v)
    {
        int i;
        float f, p, q, t;

        if (s == 0)
        {
            // achromatic (grey)
            r = g = b = v;
            return;
        }

        // sector 0 to 5
        h /= 60;
        i = (int)Math.Floor(h);

        // factorial part of h
        f = h - i;
        p = v * (1 - s);
        q = v * (1 - s * f);
        t = v * (1 - s * (1 - f));

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
    }
}
