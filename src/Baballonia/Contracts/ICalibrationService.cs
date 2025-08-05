using System.Threading.Tasks;

namespace Baballonia.Contracts;

public interface ICalibrationService
{
    Task SetExpression(string expression, float value);

    (float Lower, float Upper) GetExpressionSettings(string parameterName);

    Task<float> GetExpressionSetting(string expression);
    Task ResetValues();
    Task ResetMinimums();
    Task ResetMaximums();

}
