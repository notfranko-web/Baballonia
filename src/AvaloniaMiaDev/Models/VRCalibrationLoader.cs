// VRCalibrationModel.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvaloniaMiaDev.Models
{
    public class VRCalibrationModel : INotifyPropertyChanged
    {
        private bool _isCalibrationMode = true;
        private int _routineId = 1;
        private string _modelPath;
        private double _progress;
        private bool _isComplete;
        private string _completedModelPath;

        public bool IsCalibrationMode
        {
            get => _isCalibrationMode;
            set
            {
                if (_isCalibrationMode != value)
                {
                    _isCalibrationMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RoutineId
        {
            get => _routineId;
            set
            {
                if (_routineId != value)
                {
                    _routineId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ModelPath
        {
            get => _modelPath;
            set
            {
                if (_modelPath != value)
                {
                    _modelPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsComplete
        {
            get => _isComplete;
            set
            {
                if (_isComplete != value)
                {
                    _isComplete = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompletedModelPath
        {
            get => _completedModelPath;
            set
            {
                if (_completedModelPath != value)
                {
                    _completedModelPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
