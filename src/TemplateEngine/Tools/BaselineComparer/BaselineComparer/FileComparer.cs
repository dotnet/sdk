using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BaselineComparer
{
    public class FileComparer
    {
        private static readonly int EndExtraDisplayLength = 128;    // when one stream reaches the end, up to this many bytes of the other will be reported
        private static readonly int DifferenceCheckWindowSize = 100;
        private static readonly int PositionalSize = 10;

        private string _baselineFilePath;
        private string _checkTargetFilePath;

        private BufferedReadStream _baseline;
        private BufferedReadStream _checkTarget;

        public FileComparer(string baselineFilePath, string checkTargetFilePath)
        {
            _baselineFilePath = baselineFilePath;
            _checkTargetFilePath = checkTargetFilePath;
        }

        public IReadOnlyList<PositionalDifference> Compare()
        {
            List<PositionalDifference> differenceList = new List<PositionalDifference>();
            PositionalDifference endDifference;
            bool unhandledDifference = false;

            using (FileStream baselineStream = File.Open(_baselineFilePath, FileMode.Open))
            using (FileStream checkTargetStream = File.Open(_checkTargetFilePath, FileMode.Open))
            {
                _baseline = new BufferedReadStream(baselineStream);
                _checkTarget = new BufferedReadStream(checkTargetStream);

                do
                {
                    if (_baseline.TryReadNext(out byte baselineByte) && _checkTarget.TryReadNext(out byte targetByte))
                    {
                        if (baselineByte != targetByte)
                        {
                            if (TryFindDifferenceAtPosition(baselineByte, targetByte, out PositionalDifference difference))
                            {
                                differenceList.Add(difference);
                            }
                            else
                            {
                                PositionalDifference tooLongDifference = SetupExplicitlyTypedDifference(DifferenceCheckWindowSize, DifferenceCheckWindowSize, baselineByte, targetByte, DifferenceDatatype.TooLong, 0);
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

            // read both past their difference
            //int baselineDiffAbsoluteStart = _baseline.LastReadPosition;
            //int checkDiffAbsoluteStart = _checkTarget.LastReadPosition;
            //byte[] baselineDiffPart = _baseline.ReadBytes(baselineStartOfBestRealign, out int baselineBytesActuallyRead);
            //byte[] checkDiffPart = _checkTarget.ReadBytes(checkStartOfBestRealign, out int checkBytesActuallyRead);

            //string baselineString = Convert.ToChar(previousBaselineByte) + Encoding.Default.GetString(baselineDiffPart);
            //string checkString = Convert.ToChar(previousCheckByte) + Encoding.Default.GetString(checkDiffPart);
            //difference = new PositionalDifference(baselineDiffAbsoluteStart, baselineString, checkDiffAbsoluteStart, checkString);

            difference = SetupDifference(baselineStartOfBestRealign, checkStartOfBestRealign, previousBaselineByte, previousCheckByte);

            return true;
        }

        private PositionalDifference SetupDifference(int baselineLength, int checkLength, byte baselineDiffFirstByte, byte checkDiffFirstByte)
        {
            int baselineDiffAbsoluteStart = _baseline.LastReadPosition;
            int checkDiffAbsoluteStart = _checkTarget.LastReadPosition;
            byte[] baselineDiffPart = _baseline.ReadBytes(baselineLength, out int baselineBytesActuallyRead);
            byte[] checkDiffPart = _checkTarget.ReadBytes(checkLength, out int checkBytesActuallyRead);

            string baselineString = Convert.ToChar(baselineDiffFirstByte) + Encoding.Default.GetString(baselineDiffPart);
            string checkString = Convert.ToChar(checkDiffFirstByte) + Encoding.Default.GetString(checkDiffPart);
            return new PositionalDifference(baselineDiffAbsoluteStart, baselineString, checkDiffAbsoluteStart, checkString);
        }

        private PositionalDifference SetupExplicitlyTypedDifference(int baselineLength, int checkLength, byte baselineDiffFirstByte, byte checkDiffFirstByte, DifferenceDatatype datatype, int leeway)
        {
            int baselineDiffAbsoluteStart = _baseline.LastReadPosition;
            int checkDiffAbsoluteStart = _checkTarget.LastReadPosition;
            byte[] baselineDiffPart = _baseline.ReadBytes(baselineLength, out int baselineBytesActuallyRead);
            byte[] checkDiffPart = _checkTarget.ReadBytes(checkLength, out int checkBytesActuallyRead);

            string baselineString = Convert.ToChar(baselineDiffFirstByte) + Encoding.Default.GetString(baselineDiffPart);
            string checkString = Convert.ToChar(checkDiffFirstByte) + Encoding.Default.GetString(checkDiffPart);
            return new PositionalDifference(baselineDiffAbsoluteStart, baselineString, checkDiffAbsoluteStart, checkString, datatype, leeway);
        }

        // Starting at the current position, looks for pairs of locations in the _baseline & _checkTarget that are byte-wise equivalent.
        // These are candidiates for where the difference ends.
        private Dictionary<int, List<int>> FindPotentialRealignments()
        {
            byte[] baselineReplayBuffer = _baseline.ReadBytes(DifferenceCheckWindowSize, out int baselineBytesRead);
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

            byte[] checkReplayBuffer = _checkTarget.ReadBytes(DifferenceCheckWindowSize, out int checkBytesRead);
            Span<byte> checkSpan = checkReplayBuffer;

            // start looking for "sames" in the check buffer
            Dictionary<int, List<int>> checkStartToBaselineStartMatches = new Dictionary<int, List<int>>();
            int finalCheckSliceStart = Math.Min(baselineBytesRead, checkBytesRead) - PositionalSize;

            for (int start = 0; start < finalCheckSliceStart; start++)
            {
                Span<byte> slice = checkSpan.Slice(start, PositionalSize);
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

            _baseline.SetupReplayBytes(baselineReplayBuffer);
            _checkTarget.SetupReplayBytes(checkReplayBuffer);

            return checkStartToBaselineStartMatches;
        }

        private bool IsDoneReading(out PositionalDifference endDifference)
        {
            if (_baseline.DoneReading && _checkTarget.DoneReading)
            {
                endDifference = null;
                return true;
            }
            else if (_baseline.DoneReading)
            {
                string checkPartialEnd = ReadPartialEndData(_checkTarget);
                endDifference = new PositionalDifference(_baseline.LastReadPosition, string.Empty, _checkTarget.LastReadPosition, checkPartialEnd);
                return true;
            }
            else if (_checkTarget.DoneReading)
            {
                string baselinePartialEnd = ReadPartialEndData(_baseline);
                endDifference = new PositionalDifference(_baseline.LastReadPosition, baselinePartialEnd, _checkTarget.LastReadPosition, string.Empty);
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
