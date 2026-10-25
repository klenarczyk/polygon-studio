using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Scene Scene { get; private set; }

    [ObservableProperty]
    public partial EditorService Editor { get; private set; }

    public MainViewModel()
    {
        (Scene, Editor) = CreateSession();
    }

    [RelayCommand]
    private void Reset()
    {
        (Scene, Editor) = CreateSession();
    }

    private static (Scene Scene, EditorService Editor) CreateSession()
    {
        var scene = new Scene();
        var editor = new EditorService(scene);

        return (scene, editor);
    }
}