namespace K1.VectorDB.Graph;

public interface IGraphLayerManager
{
    IReadOnlyList<GraphLayer> Layers { get; }
    GraphLayer AddLayer(string name, string description = "");
    bool RemoveLayer(string layerId);
    GraphLayer? GetLayer(string layerId);
}
