public enum SlotsEvent
{
	// spin lifecycle
	SpinCompleted,
	ReelSpinStarted,
	ReelCompleted,
	StoppingReels,
	ReelAdded,
	ReelRemoved,

	// presentation
	BeginSlotPresentation,
	PresentationComplete,

	// symbol events
	SymbolWin,
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
	AllSlotsRemoved
}