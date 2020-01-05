using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeuralNetwork2
{
    class Layer
    {
        static private Random rand = new Random();

        public List<double> weight = new List<double>();
        public List<double> outputs = new List<double>();
        public ActivationFunctions.function func;
        public int cntNeurons;
        public int cntWeights;

        public Layer(int _cntNeurons, int sizePrevLayer, char symbol)
        {
            cntNeurons = _cntNeurons;
            func = ActivationFunctions.GetFuncActivation(symbol);
            outputs = new List<double>(cntNeurons);

            if (sizePrevLayer > 0) // Если это не входной слой НС
            {
                cntWeights = cntNeurons * sizePrevLayer;
                for (int i = 0; i < cntWeights; ++i)
                    weight.Add(GetRandomWeight());
            }
        }

        // Генерация рандомного веса в заданных промежутках
        // Здесь генерируется число в пределах от a = -0.4 до b = 0.4
        // Формула : (b - a) * rand + a;
        private double GetRandomWeight()
        {
            return 0.8 * rand.NextDouble() - 0.4;
        }
    }

    class ActivationFunctions
    {
        // Линейная ф-ия
        public static double LinearFunc(double x)
        { return x; }

        // Сигмоид
        // Её диапазон значений [0,1]
        public static double SigmoidFunc(double x)
        { return 1.0 / (1.0 + Math.Exp(-1.0 * x)); }

        // Гиперболический тангенс
        // Имеет смысл использовать гиперболический тангенс, только тогда, когда значения 
        // могут быть и отрицательными, и положительными, т.к. диапазон функции [-1,1].
        public static double HyperbolicTangentFunc(double x)
        { return (Math.Exp(2 * x) - 1.0) / (Math.Exp(2 * x) + 1.0); }


        public delegate double function(double x);
        public static function GetFuncActivation(char symbol)
        {
            if (symbol.Equals('s'))
                return SigmoidFunc;
            else
                if (symbol.Equals('t'))
                return HyperbolicTangentFunc;
            else
                return LinearFunc;
        }
    }


    class Network
    {
        private double learningRate = 0.05; // Скорость обучения

        // Слои:
        // 1. входной слой
        private Layer input;
        // 2. скрытые слои
        private List<Layer> hidden = new List<Layer>();
        // 3. выходной слой
        private Layer output;


        // Общее кол-во слоев НС
        private int cntLayers;

        // НС по умолчанию с 1 скрытым слоем
        public Network(int inputsCount, int hiddensCount, int outputCounts)
        {
            cntLayers = hiddensCount + 2;
            input = new Layer(inputsCount, 0, 'l');
            hidden.Add(new Layer(hiddensCount, inputsCount, 's'));
            output = new Layer(outputCounts, hiddensCount, 's');
        }


        public Network(int inputsCount, params int[] neuronsCount)
        {
            cntLayers = neuronsCount.Length + 1;
            input = new Layer(inputsCount, 0, 'l');

            hidden.Add(new Layer(neuronsCount[0], inputsCount, 's'));
            for (int i = 1; i < neuronsCount.Length - 1; i++)
            {
                hidden.Add(new Layer(neuronsCount[i], hidden.Last().cntNeurons, 's'));
            }

            output = new Layer(neuronsCount.Last(), hidden.Last().cntNeurons, 's');
        }

        // По значениям предыдущего слоя получаем вход для следующего (Сумма произведений сигнал * вес)
        // Манипуляции с весами, сигналами и функцией активации
        private List<double> FromInputToOutputOnNeurons(List<double> inputInfo, Layer nextLayer)
        {
            List<double> outputInfo = new List<double>(nextLayer.cntNeurons);


            for (int i = 0; i < nextLayer.cntNeurons; ++i)
            {
                double sum = 0;
                int lastInputIndex = inputInfo.Count - 1;
                for (int j = 0; j < lastInputIndex; ++j)
                {
                    double signal = inputInfo[j];
                    sum += signal * nextLayer.weight[i * inputInfo.Count + j];
                }

                // Добавляем вес последнего нейрона в слое
                sum += inputInfo[lastInputIndex] * nextLayer.weight[i * inputInfo.Count + lastInputIndex];

                outputInfo.Add(nextLayer.func(sum));
            }
            return outputInfo;
        }


        // Передача данных по слоям : выход предыдущего слоя == входу текущего
        public List<double> LayerDataTransfer(List<double> inputInfo)
        {
            // Выход входного слоя совпадаем с самим входным сигналом
            input.outputs = inputInfo;
            List<double> curInputInfo = inputInfo;

            // Работа со скрытыми слоями :
            // Выход входного слоя == входом первого скрытого слоя
            // Выход первого скрытого слоя == входом второго скрытого слоя и т.д.
            foreach (var curLayer in hidden)
            {
                curInputInfo = FromInputToOutputOnNeurons(curInputInfo, curLayer);
                curLayer.outputs = curInputInfo;
            }

            // Работа с выходным слоем : 
            output.outputs = curInputInfo = FromInputToOutputOnNeurons(curInputInfo, output);
            return curInputInfo;
        }

        // Производная функции активации
        private double GetDerivativeFuncActivation(char symbol, double outputInfo)
        {
            if (symbol.Equals('s'))
                return (1 - outputInfo) * outputInfo; // сигмоид
            else
                return 1 - Math.Pow(outputInfo, 2); // гиперболический тангенс
        }

        // Изменение весов 
        // Метод обратного распространения ошибки (один обратный проход с изменением весов и подсчетом дельт)
        private void ChangeWeights(List<double> idealOutput, List<double> outputInfo)
        {
            Layer nextLayer = output;
            List<double> deltaNext = new List<double>(idealOutput.Count);

            // Производная ф-ии активации от входного значения нейрона
            double fin = 0;

            // Находим дельту для выходного слоя
            // delta = (OUTideal - OUTcurrent) * fin
            for (int i = 0; i < idealOutput.Count; ++i)
            {
                // Производная ф-ии активации (здесь : гиперболический тангенс)
                fin = GetDerivativeFuncActivation('s', outputInfo[i]);
                deltaNext.Add((idealOutput[i] - outputInfo[i]) * fin);
            }

            // Находим дельты для скрытых слоев
            for (int i = hidden.Count - 1; i >= -1; --i)
            {
                Layer currentLayer;
                currentLayer = i != -1 ? hidden[i] : input;
                List<double> newWeights = new List<double>(nextLayer.weight.Count);
                int index = 0;

                // пересчет веса (на каждой связи нейронов текущего слоя и нейронов следующего слоя)
                for (int k = 0; k < nextLayer.cntNeurons; ++k)
                {
                    for (int j = 0; j < currentLayer.cntNeurons; ++j)
                    {
                        // градиент j, k
                        double grad = deltaNext[k] * currentLayer.outputs[j];
                        // дельта веса
                        double deltaWeight = learningRate * grad;
                        newWeights.Add(nextLayer.weight[index++] + deltaWeight);
                    }
                }
                // ---

                List<double> deltaCurrent = new List<double>(deltaNext);
                deltaNext.Clear();

                // Подстчет дельт данного слоя
                // delta = (fin * sum(W[i] * Delta[i]))
                for (int j = 0; j < currentLayer.cntNeurons; ++j)
                {
                    double sum = 0;
                    for (int k = 0; k < nextLayer.cntNeurons; ++k)
                        sum += nextLayer.weight[currentLayer.cntNeurons * k + j] * deltaCurrent[k];

                    // Производная ф-ии активации (здесь : гиперболический тангенс)
                    fin = GetDerivativeFuncActivation('s', currentLayer.outputs[j]);
                    deltaNext.Add(fin * sum);
                }
                for (int wI = 0; wI < nextLayer.cntWeights; ++wI)
                    nextLayer.weight[wI] = newWeights[wI];
                nextLayer = currentLayer;
                // ---

            }

        }

        //  Mean Squared Error
        // [(ideal[1] - current[1])^2 + ... + (ideal[n] - current[n])^2] / n
        // n - количество сетов
        private double MSE(List<double> ideal, List<double> current)
        {
            int n = ideal.Count;
            double error = 0;
            for (int i = 0; i < n; ++i)
                error += Math.Pow((ideal[i] - current[i]), 2);
            error = error / n;
            return error;
        }

        public double[] Compute(double[] _input)
        {
            List<double> inputList = new List<double>(_input);
            return LayerDataTransfer(inputList).ToArray();
        }

        public double Run(double[] _input, double[] _output)
        {
            List<double> inputList = new List<double>(_input);
            List<double> outputList = new List<double>(_output);
            List<double> output = LayerDataTransfer(inputList);
            double error = MSE(outputList, output);
            ChangeWeights(outputList, output);
            return error;
        }


        //обучение АОРО, количество образцов равно количеству ответов для них
        public double RunEpoch(double[][] _input, double[][] _output)
        {
            double error_sum = 0.0;

            for (int i = 0; i < _input.Length; i++)
            {
                error_sum += Run(_input[i], _output[i]);
            }

            return error_sum;
        }


        //Проверка корректности ответа с выходом для задач распознавания
        private bool IsCorrectAnswer(List<double> output, List<double> answer)
        {
            int patternNum = -1;

            // Поиск позиции правильного ответа в answer
            for (int i = 0; i < answer.Count; ++i)
            {
                if (answer[i] == 1)
                {
                    patternNum = i;
                    break;
                }
            }
            double maxOutSignal = double.MinValue;
            int maxOutSignalPos = -1;

            // Поиск позиции правильного ответа в output 
            // (Ищем большее значение сигнала)
            for (int i = 0; i < output.Count; ++i)
            {
                if (maxOutSignal < output[i])
                {
                    maxOutSignal = output[i];
                    maxOutSignalPos = i;
                }
            }
            return maxOutSignalPos == patternNum;
        }

        public List<double> getOutput()
        {
            return output.outputs;
        }
    }
}
