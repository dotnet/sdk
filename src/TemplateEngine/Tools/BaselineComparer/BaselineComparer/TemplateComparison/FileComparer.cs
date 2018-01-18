using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BaselineComparer.TemplateComparison
{
    public class FileComparer
    {
        private static readonly int EndExtraDisplayLength = 128;    // when one stream reaches the end, up to this many bytes of the other will be reported
        private static readonly int DifferenceCheckWindowSize = 100;
        private static readonly int PositionalSize = 10;

        private readonly string _baselineFilePath;
        private readonly string _secondaryFilePath;

        private BufferedReadStream _baselineData;
        private BufferedReadStream _secondaryData;

        public FileComparer(string baselineFilePath, string secondaryFilePath)
        {
            _baselineFilePath = baselineFilePath;
            _secondaryFilePath = secondaryFilePath;
        }

        public IReadOnlyList<PositionalDifference> Compare()
        {
            List<PositionalDifference> differenceList = new List<PositionalDifference>();
            PositionalDifference endDifference;
            bool unhandledDifference = false;

            using (FileStream baselineStream = File.Open(_baselineFilePath, FileMode.Open))
            using (FileStream secondaryStream = File.Open(_secondaryFilePath, FileMode.Open))
            {
                _baselineData = new BufferedReadStream(baselineStream);
                _secondaryData = new BufferedReadStream(secondaryStream);

                do
                {
                    if (_baselineData.TryReadNext(out byte baselineByte) && _secondaryData.TryReadNext(out byte secondaryByte))
                    {
                        if (baselineByte != secondaryByte)
                        {
                            if (TryFindDifferenceAtPosition(baselineByte, secondaryByte, out PositionalDifference difference))
                            {
                                differenceList.Add(difference);
                            }
                            else
                            {
                                PositionalDifference tooLongDifference = SetupExplicitlyTypedDifference(DifferenceCheckWindowSize, DifferenceCheckWindowSize, baselineByte, secondaryByte, DifferenceDatatype.TooLong, 0);
                                differenceList.Add(tooLongDifference);
                                unhandledDifference = true;
                            }
                        }
                    }
                } while (!IsDoneReading(out endDifference) && !unhandledDifference);
            }

            if (endDifference != null)
            {
                differenceList.Add(endDifference);
            }

            return differenceList;
        }

        // naive, greedy determination of where a content difference ends.
        private bool TryFindDifferenceAtPosition(byte previousBaselineByte, byte previousCheckByte, out PositionalDifference difference)
        {
            Dictionary<int, List<int>> checkStartToBaselineStartMatches = FindPotentialRealignments();

            if (checkStartToBaselineStartMatches.Count == 0)
            {
                difference = null;
                return false;
            }

            // the best difference is the one that has the smallest sum difference of start positions (sum of squares may yield a better result, may have to experiment)
            int bestStartDelta = int.MaxValue;
            int baselineStartOfBestRealign = int.MaxValue;
            int checkStartOfBestRealign = int.MaxValue;

            foreach (KeyValuePair<int, List<int>> candidate in checkStartToBaselineStartMatches)
            {
                int checkStartPosition = candidate.Key;
                foreach (int baselineStartPosition in candidate.Value)
                {
                    int startDelta = checkStartPosition + baselineStartPosition;

                    if (startDelta < bestStartDelta)
                    {
                        bestStartDelta = startDelta;
                        baselineStartOfBestRealign = baselineStartPosition;
                        checkStartOfBestRealign = checkStartPosition;
                    }
                    else if ((startDelta == bestStartDelta) && (baselineStartOfBestRealign < baselineStartPosition))
                    {
                        baselineStartOfBestRealign = baselineStartPosition;
                        checkStartOfBestRealign = checkStartPosition;
                    }
                }
            }

            if (bestStartDelta == int.MaxValue)
            {   // shouldn't be possible,  but good to check while developing
                difference = null;
                return false;
            }

            difference = SetupDifference(baselineStartOfBestRealign, checkStartOfBestRealign, previousBaselineByte, previousCheckByte);

            return true;
        }

        private PositionalDifference SetupDifference(int baselineLength, int checkLength, byte baselineDiffFirstByte, byte checkDiffFirstByte)
        {
            int baselineDiffAbsoluteStart = _baselineData.LastReadPosition;
            int checkDiffAbsoluteStart = _secondaryData.LastReadPosition;
            byte[] baselineDiffPart = _baselineData.ReadBytes(baselineLength, out int baselineBytesActuallyRead);
            byte[] checkDiffPart = _secondaryData.ReadBytes(checkLength, out int checkBytesActuallyRead);

            string baselineString = Convert.ToChar(baselineDiffFirstByte) + Encoding.Default.GetString(baselineDiffPart);
            string checkString = Convert.ToChar(checkDiffFirstByte) + Encoding.Default.GetString(checkDiffPart);
            return new PositionalDifference(baselineDiffAbsoluteStart, baselineString, checkDiffAbsoluteStart, checkString);
        }

        private PositionalDifference SetupExplicitlyTypedDifference(int baselineLength, int checkLength, byte baselineDiffFirstByte, byte checkDiffFirstByte, DifferenceDatatype datatype, int leeway)
        {
            int baselineDiffAbsoluteStart = _baselineData.LastReadPosition;
            int checkDiffAbsoluteStart = _secondaryData.LastReadPosition;
            byte[] baselineDiffPart = _baselineData.ReadBytes(baselineLength, out int baselineBytesActuallyRead);
            byte[] checkDiffPart = _secondaryData.ReadBytes(checkLength, out int checkBytesActuallyRead);

            string baselineString = Convert.ToChar(baselineDiffFirstByte) + Encoding.Default.GetString(baselineDiffPart);
            string checkString = Convert.ToChar(checkDiffFirstByte) + Encoding.Default.GetString(checkDiffPart);
            return new PositionalDifference(baselineDiffAbsoluteStart, baselineString, checkDiffAbsoluteStart, checkString, datatype, leeway);
        }

        // Starting at the current position, looks for pairs of locations in the _baselineData & _secondaryData that are byte-wise equivalent.
        // These are candidiates for where the difference ends.
        private Dictionary<int, List<int>> FindPotentialRealignments()
        {
            byte[] baselineReplayBuffer = _baselineData.ReadBytes(DifferenceCheckWindowSize, out int baselineBytesRead);
            Span<byte> baselineSpan = baselineReplayBuffer;

            // setup the baseline lookups
            Dictionary<string, IList<int>> baselinePositionals = new Dictionary<string, IList<int>>(StringComparer.Ordinal);
            for (int start = 0; start < baselineBytesRead - PositionalSize; start++)
            {
                Span<byte> slice = baselineSpan.Slice(start, PositionalSize);
                string sliceString = Encoding.Default.GetString(slice.ToArray());
                if (!baselinePositionals.TryGetValue(sliceString, out IList<int> positions))
                {
                    positions = new List<int>();
                    baselinePositionals[sliceString] = positions;
                }

                positions.Add(start);
            }

            byte[] secondaryReplayBuffer = _secondaryData.ReadBytes(DifferenceCheckWindowSize, out int checkBytesRead);
            Span<byte> secondarySpan = secondaryReplayBuffer;

            // start looking for "sames" in the check buffer
            Dictionary<int, List<int>> checkStartToBaselineStartMatches = new Dictionary<int, List<int>>();
            int finalCheckSliceStart = Math.Min(baselineBytesRead, checkBytesRead) - PositionalSize;

            for (int start = 0; start < finalCheckSliceStart; start++)
            {
                Span<byte> slice = secondarySpan.Slice(start, PositionalSize);
                string sliceString = Encoding.Default.GetString(slice.ToArray());
                if (baselinePositionals.TryGetValue(sliceString, out IList<int> baselineMatchPositions))
                {
                    if (!checkStartToBaselineStartMatches.TryGetValue(start, out List<int> baselineStartPositions))
                    {
                        baselineStartPositions = new List<int>();
                        checkStartToBaselineStartMatches[start] = baselineStartPositions;
                    }

                    baselineStartPositions.AddRange(baselineMatchPositions);
                }
            }

            _baselineData.SetupReplayBytes(baselineReplayBuffer);
            _secondaryData.SetupReplayBytes(secondaryReplayBuffer);

            return checkStartToBaselineStartMatches;
        }

        private bool IsDoneReading(out PositionalDifference endDifference)
        {
            if (_baselineData.DoneReading && _secondaryData.DoneReading)
            {
                endDifference = null;
                return true;
            }
            else if (_baselineData.DoneReading)
            {
                string checkPartialEnd = ReadPartialEndData(_secondaryData);
                endDifference = new PositionalDifference(_baselineData.LastReadPosition, string.Empty, _secondaryData.LastReadPosition, checkPartialEnd);
                return true;
            }
            else if (_secondaryData.DoneReading)
            {
                string baselinePartialEnd = ReadPartialEndData(_baselineData);
                endDifference = new PositionalDifference(_baselineData.LastReadPosition, baselinePartialEnd, _secondaryData.LastReadPosition, string.Empty);
                return true;
            }

            endDifference = null;
            return false;
        }

        private static string ReadPartialEndData(BufferedReadStream stream)
        {
            byte[] endPartialBuffer = new byte[EndExtraDisplayLength];
            int bytesRead = 0;
            while (stream.TryReadNext(out byte nextByte) && bytesRead < EndExtraDisplayLength)
            {
                endPartialBuffer[bytesRead] = nextByte;
                ++bytesRead;
            }

            if (bytesRead == EndExtraDisplayLength)
            {
                return Convert.ToString(endPartialBuffer);
            }
            else
            {
                return Convert.ToString(endPartialBuffer) + "...";
            }
        }
    }
}
