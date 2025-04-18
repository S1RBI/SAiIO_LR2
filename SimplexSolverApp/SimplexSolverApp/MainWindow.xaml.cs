using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;


namespace SimplexSolverApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        private void SolveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultsTextBox.Clear();

                if (!ParseInput(out double[] objectiveCoeffs, out List<double[]> constraintCoeffsList, out List<double> rhsList))
                {
                    return;
                }

                int numVars = objectiveCoeffs.Length;
                int numConstraints = constraintCoeffsList.Count;

                StringBuilder resultBuilder = new StringBuilder();
                resultBuilder.AppendLine("--- Прямая задача (Исходная) ---");
                resultBuilder.Append("Максимизировать Z = ");
                for (int i = 0; i < numVars; i++)
                {
                    resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}{1:F4}*x{2}", objectiveCoeffs[i] >= 0 && i > 0 ? " + " : " ", objectiveCoeffs[i], i + 1);
                }
                resultBuilder.AppendLine();
                resultBuilder.AppendLine("При ограничениях:");
                for (int i = 0; i < numConstraints; i++)
                {
                    for (int j = 0; j < numVars; j++)
                    {
                        resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}{1:F4}*x{2}", constraintCoeffsList[i][j] >= 0 && j > 0 ? " + " : " ", constraintCoeffsList[i][j], j + 1);
                    }
                    resultBuilder.AppendLine($" <= {rhsList[i]:F4}");
                }
                resultBuilder.AppendLine("x_i >= 0 для всех i = 1.." + numVars);
                resultBuilder.AppendLine("\n");

                int numSlackVars = numConstraints;
                int totalVars = numVars + numSlackVars;
                int numRows = numConstraints + 1;
                int numCols = totalVars + 1;

                double[,] tableau = new double[numRows, numCols];

                for (int i = 0; i < numConstraints; i++)
                {
                    for (int j = 0; j < numVars; j++)
                    {
                        tableau[i, j] = constraintCoeffsList[i][j];
                    }
                    tableau[i, numVars + i] = 1;
                    tableau[i, numCols - 1] = rhsList[i];
                }

                int objectiveRowIndex = numRows - 1;
                for (int j = 0; j < numVars; j++)
                {
                    tableau[objectiveRowIndex, j] = -objectiveCoeffs[j];
                }
                tableau[objectiveRowIndex, numCols - 1] = 0;

                int[] basicVariables = new int[numConstraints];
                for (int i = 0; i < numConstraints; ++i)
                {
                    basicVariables[i] = numVars + i;
                }

                resultBuilder.AppendLine("--- Симплекс-итерации ---");
                bool unbounded = false;
                int iterations = 0;
                const int MAX_ITERATIONS = 100;

                while (iterations < MAX_ITERATIONS)
                {
                    resultBuilder.AppendLine($"\nИтерация {iterations + 1}:");
                    AppendTableau(resultBuilder, tableau, numRows, numCols, numVars, numSlackVars, basicVariables);

                    int pivotCol = -1;
                    double minObjectiveCoeff = -1e-9;
                    for (int j = 0; j < totalVars; j++)
                    {
                        if (tableau[objectiveRowIndex, j] < minObjectiveCoeff)
                        {
                            minObjectiveCoeff = tableau[objectiveRowIndex, j];
                            pivotCol = j;
                        }
                    }

                    if (pivotCol == -1)
                    {
                        resultBuilder.AppendLine("\nОптимальное решение найдено.");
                        break;
                    }
                    string enteringVarName = (pivotCol < numVars) ? $"x{pivotCol + 1}" : $"s{pivotCol - numVars + 1}";
                    resultBuilder.AppendLine($"Ведущий столбец (входящая переменная): {enteringVarName} (индекс {pivotCol})");

                    int pivotRow = -1;
                    double minRatio = double.MaxValue;
                    unbounded = true;

                    for (int i = 0; i < numConstraints; i++)
                    {
                        double colValue = tableau[i, pivotCol];
                        double rhsValue = tableau[i, numCols - 1];

                        if (colValue > 1e-9)
                        {
                            unbounded = false;
                            double ratio = rhsValue / colValue;

                            if (ratio < minRatio && ratio >= 0)
                            {
                                minRatio = ratio;
                                pivotRow = i;
                            }
                        }
                    }

                    if (unbounded)
                    {
                        resultBuilder.AppendLine("\nЗадача НЕ ОГРАНИЧЕНА (нет положительных элементов в ведущем столбце).");
                        break;
                    }

                    string leavingVarName = (basicVariables[pivotRow] < numVars) ? $"x{basicVariables[pivotRow] + 1}" : $"s{basicVariables[pivotRow] - numVars + 1}";
                    resultBuilder.AppendLine($"Ведущая строка (выходящая переменная): {leavingVarName} (Базисная переменная в строке {pivotRow + 1}, индекс строки {pivotRow})");
                    resultBuilder.AppendLine($"Разрешающий элемент: {tableau[pivotRow, pivotCol]:F4}");

                    double pivotElement = tableau[pivotRow, pivotCol];

                    basicVariables[pivotRow] = pivotCol;

                    for (int j = 0; j < numCols; j++)
                    {
                        tableau[pivotRow, j] /= pivotElement;
                    }

                    for (int i = 0; i < numRows; i++)
                    {
                        if (i != pivotRow)
                        {
                            double factor = tableau[i, pivotCol];
                            for (int j = 0; j < numCols; j++)
                            {
                                tableau[i, j] -= factor * tableau[pivotRow, j];
                            }
                        }
                    }

                    iterations++;
                    if (iterations == MAX_ITERATIONS)
                    {
                        resultBuilder.AppendLine($"\nДостигнуто максимальное число итераций ({MAX_ITERATIONS}). Остановка.");
                    }
                }

                resultBuilder.AppendLine("\n--- Решение прямой задачи ---");
                if (unbounded)
                {
                    resultBuilder.AppendLine("Задача не ограничена, нет конечного оптимального решения.");
                }
                else if (iterations == MAX_ITERATIONS)
                {
                    resultBuilder.AppendLine("Остановка из-за превышения числа итераций. Решение может быть не оптимальным.");
                    DisplaySolution(resultBuilder, tableau, objectiveRowIndex, numCols, numVars, numConstraints, basicVariables, true);
                }
                else
                {
                    DisplaySolution(resultBuilder, tableau, objectiveRowIndex, numCols, numVars, numConstraints, basicVariables, true);

                    resultBuilder.AppendLine("\n--- Двойственная задача ---");
                    resultBuilder.Append("Минимизировать W = ");
                    for (int i = 0; i < numConstraints; ++i)
                    {
                        resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}{1:F4}*y{2}", rhsList[i] >= 0 && i > 0 ? " + " : " ", rhsList[i], i + 1);
                    }
                    resultBuilder.AppendLine();
                    resultBuilder.AppendLine("При ограничениях:");
                    for (int j = 0; j < numVars; ++j)
                    {
                        for (int i = 0; i < numConstraints; ++i)
                        {
                            double coeff = constraintCoeffsList[i][j];
                            resultBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0}{1:F4}*y{2}", coeff >= 0 && i > 0 ? " + " : " ", coeff, i + 1);
                        }
                        resultBuilder.AppendLine($" >= {objectiveCoeffs[j]:F4}");
                    }
                    resultBuilder.AppendLine("y_i >= 0 для всех i = 1.." + numConstraints);
                    resultBuilder.AppendLine("\n");

                    resultBuilder.AppendLine("\n--- Решение двойственной задачи ---");
                    double[] dualSolution = new double[numConstraints];
                    for (int i = 0; i < numConstraints; ++i)
                    {
                        int slackVarColIndex = numVars + i;
                        dualSolution[i] = tableau[objectiveRowIndex, slackVarColIndex];
                        resultBuilder.AppendLine($"y{i + 1} = {dualSolution[i]:F4}");
                    }
                    double dualOptimalValue = tableau[objectiveRowIndex, numCols - 1];
                    resultBuilder.AppendLine($"Оптимальное W (Мин) = {dualOptimalValue:F4}");

                    resultBuilder.AppendLine("\n--- Проверка теорем двойственности ---");
                    double primalOptimalValue = tableau[objectiveRowIndex, numCols - 1];
                    resultBuilder.AppendLine($"Оптимальное Z прямой задачи (Макс) = {primalOptimalValue:F4}");
                    resultBuilder.AppendLine($"Оптимальное W двойственной задачи (Мин) = {dualOptimalValue:F4}");

                    if (Math.Abs(primalOptimalValue - dualOptimalValue) < 1e-6)
                    {
                        resultBuilder.AppendLine("Первая теорема двойственности выполняется: Оптимальные значения Z и W равны.");
                    }
                    else
                    {
                        resultBuilder.AppendLine("ВНИМАНИЕ: Обнаружено расхождение между оптимальными значениями Z и W. Проверьте вычисления или точность.");
                    }

                    resultBuilder.AppendLine("\nПроверка второй теоремы двойственности (дополняющая нежесткость):");
                    bool secondTheoremHolds = true;

                    double[] primalSolution = GetPrimalSolution(tableau, numCols, numVars, numConstraints, basicVariables);
                    for (int j = 0; j < numVars; ++j)
                    {
                        if (primalSolution[j] > 1e-6)
                        {
                            double dualConstraintLHS = 0;
                            for (int i = 0; i < numConstraints; ++i)
                            {
                                dualConstraintLHS += constraintCoeffsList[i][j] * dualSolution[i];
                            }
                            if (Math.Abs(dualConstraintLHS - objectiveCoeffs[j]) > 1e-6)
                            {
                                resultBuilder.AppendLine($" - Нарушение для x{j + 1} > 0: Ограничение двойственной задачи {j + 1} не выполняется как равенство ({dualConstraintLHS:F4} != {objectiveCoeffs[j]:F4})");
                                secondTheoremHolds = false;
                            }
                            else
                            {
                                resultBuilder.AppendLine($" - Для x{j + 1} = {primalSolution[j]:F4} > 0: Ограничение двойственной задачи {j + 1} выполняется как равенство ({dualConstraintLHS:F4} ≈ {objectiveCoeffs[j]:F4})");
                            }
                        }
                    }

                    double[] slackValues = GetSlackValues(tableau, numCols, numVars, numConstraints, basicVariables);
                    for (int i = 0; i < numConstraints; ++i)
                    {
                        if (dualSolution[i] > 1e-6)
                        {
                            if (Math.Abs(slackValues[i]) > 1e-6)
                            {
                                resultBuilder.AppendLine($" - Нарушение для y{i + 1} > 0: Ограничение прямой задачи {i + 1} не выполняется как равенство (s{i + 1} = {slackValues[i]:F4} != 0)");
                                secondTheoremHolds = false;
                            }
                            else
                            {
                                resultBuilder.AppendLine($" - Для y{i + 1} = {dualSolution[i]:F4} > 0: Ограничение прямой задачи {i + 1} выполняется как равенство (s{i + 1} = {slackValues[i]:F4} ≈ 0)");
                            }
                        }
                    }

                    if (secondTheoremHolds)
                    {
                        resultBuilder.AppendLine("Вторая теорема двойственности (условия дополняющей нежесткости) выполняются.");
                    }
                    else
                    {
                        resultBuilder.AppendLine("ВНИМАНИЕ: Обнаружены нарушения второй теоремы двойственности.");
                    }
                }

                ResultsTextBox.Text = resultBuilder.ToString();

            }
            catch (FormatException ex)
            {
                MessageBox.Show($"Ошибка формата числа: Проверьте правильность ввода чисел (разделитель - точка или запятая в зависимости от настроек системы).\n{ex.Message}",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                ResultsTextBox.Text = $"Ошибка разбора ввода: {ex.Message}";
            }
            catch (IndexOutOfRangeException ex)
            {
                MessageBox.Show($"Ошибка структуры данных: Несоответствие количества коэффициентов или другие проблемы индексации.\n{ex.Message}",
                                "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                ResultsTextBox.Text = $"Ошибка обработки структуры ввода: {ex.Message}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла непредвиденная ошибка:\n{ex.Message}\n{ex.StackTrace}",
                                "Ошибка выполнения", MessageBoxButton.OK, MessageBoxImage.Error);
                ResultsTextBox.Text = $"Ошибка выполнения: {ex.ToString()}";
            }
        }

        private bool ParseInput(out double[] objectiveCoeffs, out List<double[]> constraintCoeffsList, out List<double> rhsList)
        {
            objectiveCoeffs = null;
            constraintCoeffsList = new List<double[]>();
            rhsList = new List<double>();
            int numVars = 0;

            try
            {
                string[] objectiveParts = ObjectiveFunctionTextBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                objectiveCoeffs = new double[objectiveParts.Length];
                if (objectiveParts.Length == 0)
                {
                    MessageBox.Show("Целевая функция не может быть пустой.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                for (int i = 0; i < objectiveParts.Length; ++i)
                {
                    if (!double.TryParse(objectiveParts[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out objectiveCoeffs[i]))
                    {
                        MessageBox.Show($"Не удалось преобразовать '{objectiveParts[i]}' в число в целевой функции.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
                numVars = objectiveCoeffs.Length;

                string[] constraintLines = ConstraintsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (constraintLines.Length == 0)
                {
                    MessageBox.Show("Введите хотя бы одно ограничение.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                foreach (string line in constraintLines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    string[] parts = trimmedLine.Split(',');
                    int expectedParts = numVars + 2;

                    for (int i = 0; i < parts.Length; ++i)
                    {
                        parts[i] = parts[i].Trim();
                    }

                    if (parts.Length != expectedParts)
                    {
                        MessageBox.Show($"Ошибка в строке ограничения: '{trimmedLine}'.\nОжидалось {expectedParts} частей ( {numVars} коэфф., знак '<=', правая часть), но получено {parts.Length}.",
                                        "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    double[] coeffs = new double[numVars];
                    for (int i = 0; i < numVars; i++)
                    {
                        if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out coeffs[i]))
                        {
                            MessageBox.Show($"Не удалось преобразовать '{parts[i]}' в число в ограничении: '{trimmedLine}'.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }
                    constraintCoeffsList.Add(coeffs);

                    string sign = parts[numVars];
                    if (sign != "<=")
                    {
                        MessageBox.Show($"В ограничении '{trimmedLine}' используется неподдерживаемый оператор '{sign}'. Поддерживается только '<='.",
                                        "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }

                    double rhs;
                    if (!double.TryParse(parts[numVars + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out rhs))
                    {
                        MessageBox.Show($"Не удалось преобразовать правую часть '{parts[numVars + 1]}' в число в ограничении: '{trimmedLine}'.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    if (rhs < 0)
                    {
                        MessageBox.Show($"В ограничении '{trimmedLine}' отрицательная правая часть ({rhs}). Эта реализация требует неотрицательных правых частей.",
                                        "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    rhsList.Add(rhs);
                }

                return true;
            }
            catch (FormatException ex)
            {
                MessageBox.Show($"Неверный формат числа при вводе: {ex.Message}", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка разбора ввода: {ex.Message}", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void AppendTableau(StringBuilder sb, double[,] tableau, int rows, int cols, int numVars, int numSlacks, int[] basicVars)
        {
            int objectiveRowIndex = rows - 1;

            sb.Append("Базис |");
            for (int j = 0; j < numVars; ++j) sb.Append($"     x{j + 1} |");
            for (int j = 0; j < numSlacks; ++j) sb.Append($"     s{j + 1} |");
            sb.AppendLine("      ПЧ |");
            sb.AppendLine(new string('-', 7 + cols * 9));

            for (int i = 0; i < objectiveRowIndex; i++)
            {
                int basisVarIndex = basicVars[i];
                string basicVarName = (basisVarIndex < numVars)
                                    ? $"x{basisVarIndex + 1}"
                                    : $"s{basisVarIndex - numVars + 1}";
                sb.AppendFormat("{0,-6}|", basicVarName);

                for (int j = 0; j < cols; j++)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0,8:F2} |", tableau[i, j]);
                }
                sb.AppendLine();
            }

            sb.Append("Z     |");
            for (int j = 0; j < cols; ++j)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,8:F2} |", tableau[objectiveRowIndex, j]);
            }
            sb.AppendLine();
            sb.AppendLine(new string('-', 7 + cols * 9));

            sb.Append("Текущие оценки двойств. переменных (из Z-строки): y = [");
            bool first = true;
            for (int j = 0; j < numSlacks; ++j)
            {
                int slackVarColIndex = numVars + j;
                double dualValue = tableau[objectiveRowIndex, slackVarColIndex];
                if (!first) sb.Append(", ");
                sb.AppendFormat(CultureInfo.InvariantCulture, "y{0}={1:F4}", j + 1, dualValue);
                first = false;
            }
            sb.AppendLine("]");
        }

        private void DisplaySolution(StringBuilder sb, double[,] tableau, int objectiveRowIndex, int numCols, int numVars, int numConstraints, int[] basicVariables, bool isPrimal)
        {
            if (isPrimal)
            {
                double[] solution = GetPrimalSolution(tableau, numCols, numVars, numConstraints, basicVariables);
                double objectiveValue = tableau[objectiveRowIndex, numCols - 1];

                sb.AppendLine($"Оптимальное Z (Макс) = {objectiveValue:F4}");
                for (int i = 0; i < numVars; i++)
                {
                    sb.AppendLine($"x{i + 1} = {solution[i]:F4}");
                }

                sb.AppendLine("Значения балансовых переменных:");
                double[] slackValues = GetSlackValues(tableau, numCols, numVars, numConstraints, basicVariables);
                for (int i = 0; i < numConstraints; i++)
                {
                    sb.AppendLine($"s{i + 1} = {slackValues[i]:F4} {(IsSlackBasic(i, numVars, basicVariables) ? "(Базисная)" : "(Небазисная)")}");
                }
            }
        }

        private double[] GetPrimalSolution(double[,] tableau, int numCols, int numVars, int numConstraints, int[] basicVariables)
        {
            double[] solution = new double[numVars];
            int rhsCol = numCols - 1;

            for (int i = 0; i < numConstraints; ++i)
            {
                int basicVarIndex = basicVariables[i];
                if (basicVarIndex < numVars)
                {
                    solution[basicVarIndex] = tableau[i, rhsCol];
                }
            }
            return solution;
        }

        private double[] GetSlackValues(double[,] tableau, int numCols, int numVars, int numConstraints, int[] basicVariables)
        {
            double[] slacks = new double[numConstraints];
            int rhsCol = numCols - 1;

            for (int i = 0; i < numConstraints; ++i)
            {
                int basicVarIndex = basicVariables[i];
                if (basicVarIndex >= numVars)
                {
                    int slackIndex = basicVarIndex - numVars;
                    slacks[slackIndex] = tableau[i, rhsCol];
                }
            }
            return slacks;
        }

        private bool IsSlackBasic(int slackIndex, int numVars, int[] basicVariables)
        {
            int targetVarIndex = numVars + slackIndex;
            return basicVariables.Contains(targetVarIndex);
        }
    }
}