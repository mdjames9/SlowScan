    /// <summary>
    /// The types of tones we can look for that mean something.
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// Used to give us something else to reference.
        /// </summary>
        NoiseLow,

        /// <summary>
        /// This tone means a logic 1
        /// </summary>
        One,

        /// <summary>
        /// This tone is used as a start bit for the VIS code.
        /// </summary>
        Break,

        /// <summary>
        /// This tone indicates a logic zero.
        /// </summary>
        Zero,

        /// <summary>
        /// This tone indicates an HSYNC.
        /// </summary>
        HSync,

        /// <summary>
        /// This is the lead tone identifying an SSTV signal.
        /// </summary>
        Leader,

        /// <summary>
        /// Also used as a reference tone.
        /// </summary>
        NoiseHigh,
    }