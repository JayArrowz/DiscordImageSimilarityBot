namespace ImageSimilarityBot.Model;

public class AIConfig
{
    public string ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public int VectorDimensions { get; set; }
    public string Model { get; set; }
    public double ActionableThreshold { get; set; }
    public bool Bannable { get; set; }
}
