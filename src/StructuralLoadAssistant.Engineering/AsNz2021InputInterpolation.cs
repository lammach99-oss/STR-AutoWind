namespace StructuralLoadAssistant.Engineering;
/// <summary>Interpolation for supplied, authorized operands; no standard table content or applicability inference.</summary>
public static class AsNz2021InputInterpolation
{
    public static double Linear(double x,double x0,double x1,double y0,double y1)
    {
        if(new[]{x,x0,x1,y0,y1}.Any(v=>!double.IsFinite(v)) || x1<=x0 || x<x0 || x>x1)
            throw new ArgumentOutOfRangeException(nameof(x),"Finite ordered bounds required; extrapolation prohibited.");
        return y0+(y1-y0)*(x-x0)/(x1-x0);
    }
    public static double Terrain(double height,double terrain,IReadOnlyList<double> heights,IReadOnlyList<double> categories,double[,] suppliedGrid)
    {
        if(heights.Count<2||categories.Count<2||suppliedGrid.GetLength(0)!=heights.Count||suppliedGrid.GetLength(1)!=categories.Count)
            throw new ArgumentException("Grid dimensions do not match supplied axes.");
        int Bracket(IReadOnlyList<double> axis,double value)
        {
            if(!double.IsFinite(value)||axis.Any(v=>!double.IsFinite(v))||Enumerable.Range(1,axis.Count-1).Any(i=>axis[i]<=axis[i-1])||value<axis[0]||value>axis[^1])
                throw new ArgumentOutOfRangeException(nameof(value));
            int index=0;while(index<axis.Count-2&&axis[index+1]<value)index++;return index;
        }
        int h=Bracket(heights,height),c=Bracket(categories,terrain);
        double lower=Linear(height,heights[h],heights[h+1],suppliedGrid[h,c],suppliedGrid[h+1,c]);
        double upper=Linear(height,heights[h],heights[h+1],suppliedGrid[h,c+1],suppliedGrid[h+1,c+1]);
        return Linear(terrain,categories[c],categories[c+1],lower,upper);
    }
}
