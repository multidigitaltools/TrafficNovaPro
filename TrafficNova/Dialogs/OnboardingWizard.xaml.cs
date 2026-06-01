using System.Windows;
using System.Windows.Controls;

namespace TrafficNova.Dialogs;

public partial class OnboardingWizard : Window
{
    private int _step = 1;
    private const int TotalSteps = 4;

    private readonly StackPanel[] _steps;

    public OnboardingWizard()
    {
        InitializeComponent();
        _steps = [Step1, Step2, Step3, Step4];
        UpdateUI();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step < TotalSteps)
        {
            _step++;
            UpdateUI();
        }
        else
        {
            Close();
        }
    }

    private void OnPrev(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            UpdateUI();
        }
    }

    private void OnSkip(object sender, RoutedEventArgs e) => Close();

    private void UpdateUI()
    {
        for (int i = 0; i < _steps.Length; i++)
            _steps[i].Visibility = (i + 1) == _step ? Visibility.Visible : Visibility.Collapsed;

        StepIndicator.Text  = $"Step {_step} of {TotalSteps}";
        PrevButton.Visibility = _step > 1 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content   = _step == TotalSteps ? "Get Started" : "Next →";
        SkipButton.Visibility = _step < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
    }
}
