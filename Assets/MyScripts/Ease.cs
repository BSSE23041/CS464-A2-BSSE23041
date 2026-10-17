
public static class Ease
{
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float u = t - 1f;

        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
