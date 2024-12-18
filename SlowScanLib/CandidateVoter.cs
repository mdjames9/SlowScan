public class CandidateVoter
{

    /// <summary>
    /// Signal start indices.
    /// </summary>
    private int[] _StartIndexes;

    /// <summary>
    /// Data indicating which tone was heard at certain times.
    /// </summary>
    private FilterType[] _Data;

    /// <summary>
    /// If true, the data is full and the next added sample will kick out the first one.
    /// </summary>
    private bool _IsFull;

    /// <summary>
    /// The current data slot.
    /// </summary>
    private int _CurrentDataIndex;

    /// <summary>
    /// The index of the last available sample slot.
    /// </summary>
    private int _MaxDataIndex;

    /// <summary>
    /// The number of different candidates we will consider before voting.
    /// </summary>
    private int _MaxCandidates;

    /// <summary>
    /// How many big samples are required for a signal candidate.
    /// </summary>
    private int _CandidateDataSize;

    /// <summary>
    /// The tone that we are trying to identify amongst the noise.
    /// </summary>
    private FilterType _Tone;
 
    /// <summary>
    /// Indicates that the criteria for considering a matched signal have been met.
    /// </summary>
    public event EventHandler<int>? MatchFound;

    /// <summary>
    /// Creates a candidate voter.
    /// </summary>
    /// <param name="toneToFind">The tone to find.</param>
    /// <param name="signalLengthMs">How long the tone should be. (Divisible by 10, minimum 10.</param>
    /// <exception cref="ArgumentException">The signal length was unsupported.</exception>
    public CandidateVoter(FilterType toneToFind, int signalLengthMs)
    {
        if(signalLengthMs < 10 || signalLengthMs % 10 != 0)
        {
            throw new ArgumentException("Not supported.");
        }
        _Tone = toneToFind;
        _CurrentDataIndex = 0;
        _IsFull = false;
        _CandidateDataSize = signalLengthMs / 10;
        
        _MaxCandidates = _CandidateDataSize / 2;

        _MaxDataIndex = _CandidateDataSize + _MaxCandidates;
        
        //Edge case for 10ms signal.
        if (_MaxCandidates == 0)
        {
            _MaxCandidates = 1;
        }
        _Data = new FilterType[_MaxDataIndex];
        _StartIndexes = new int[_MaxDataIndex];
    }

    /// <summary>
    /// Adds a new block of data to the data collection.
    /// </summary>
    /// <param name="winner">The tone that won the last Goertzel match.</param>
    /// <param name="startIndex">Index used for the start of the FFT.</param>
    public void AddBlock(FilterType winner, int startIndex)
    {
        if(!_IsFull)
        {
            _Data[_CurrentDataIndex] = winner;
            _StartIndexes[_CurrentDataIndex] = startIndex;
            _CurrentDataIndex++;
        }
        else
        {
            Array.Copy(_Data, 1, _Data, 0, _Data.Length -1);
            Array.Copy(_StartIndexes, 1, _StartIndexes, 0, _StartIndexes.Length -1);
            _Data[_CurrentDataIndex - 1] = winner;
            _StartIndexes[_CurrentDataIndex - 1] = startIndex;
            CheckForMatchAndVote(); 
        }
        if(!_IsFull && _CurrentDataIndex == _MaxDataIndex)
        {
            _IsFull = true;
            CheckForMatchAndVote();
        }

    }
    
    /// <summary>
    /// Checks to see if a match was successful.
    /// </summary>
    public void CheckForMatchAndVote()
    {
        if(!_IsFull)
        {
            return;
        }
        int[] votes = new int[_MaxCandidates];
        for(int i = 0; i < _MaxDataIndex; i++)
        {
            if (_Data[i] == _Tone)
            {
                for(int j = 0; j < _MaxCandidates; j++)
                {
                    if(j <= i)
                    {
                        if(i < j + _CandidateDataSize)
                        {
                            votes[j]++;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        int maxVotesIndex = -1;
        int maxVotes = 0;
        for(int i = 0; i < _MaxCandidates; i++)
        {
            if(votes[i] > maxVotes)
            {
                maxVotes = votes[i];
                maxVotesIndex = i;
            }
        }
        if(maxVotesIndex != -1 && maxVotes >= (_CandidateDataSize * 9) / 10)
        {
            MatchFound?.Invoke(this, _StartIndexes[maxVotesIndex]);
        }
    }
}
