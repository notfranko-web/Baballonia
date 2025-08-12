using CommunityToolkit.Mvvm.ComponentModel;

namespace Baballonia.Models;

public partial class SliderBindableSetting : ObservableObject
{
    public string Name { get; set; }
    [ObservableProperty] private float _lower;

    [ObservableProperty] private float _upper;

    public SliderBindableSetting(string name, float lower = 0, float upper = 1)
    {
        Name = name;
        Lower = lower;
        Upper = upper;
    }
}
