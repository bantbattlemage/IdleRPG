public enum SlotsEvent
{
	// spin lifecycle
	SpinCompleted,
	ReelSpinStarted,
	ReelCompleted,
	StoppingReels,
	ReelAdded,
	ReelRemoved,

	// reel-strip updates
	ReelStripUpdated,

	// presentation
	BeginSlotPresentation,
	PresentationComplete,
	// group-level presentation finished across all participating slots
	PresentationCompleteGroup,

	// symbol events
	SymbolLanded,

	// global UI/input
	BetChanged,
	CreditsChanged,
	SpinButtonPressed,
	StopButtonPressed,
	BetUpPressed,
	BetDownPressed,
	PlayerInputPressed,

	// slot management
	AllSlotsRemoved,

	// Engine-level: all reels have signalled that their spin started
	ReelsAllStarted
}