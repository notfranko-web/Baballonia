using System.Threading.Tasks;
using Baballonia.Services.Calibration;

namespace Baballonia.Contracts;

public interface ICalibrationService
{
    Task SetExpression(string expression, float value);

    CalibrationParameter GetExpressionSettings(string parameterName);

    Task<float> GetExpressionSetting(string expression);
    Task ResetValues();
    Task ResetMinimums();
    Task ResetMaximums();

}
