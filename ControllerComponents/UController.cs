namespace UnscriptedEngine
{
    public abstract class UController : ULevelPawn
    {
        protected ULevelPawn possessedPawn;

        protected override void OnLevelStarted()
        {
            base.OnLevelStarted();

            PossessPawn(GameMode.GetPlayerPawn(), true);

            OnPawnToBeDestroyed += Controller_OnPawnDestroyed;
        }

        private void Controller_OnPawnDestroyed(object sender, System.EventArgs e)
        {
            ULevelPawn levelPawn = sender as ULevelPawn;
            if (levelPawn == possessedPawn)
            {
                UnPossessPawn();
            }
        }

        protected override void OnLevelStopped()
        {
            OnPawnToBeDestroyed -= Controller_OnPawnDestroyed;

            UnPossessPawn();

            base.OnLevelStopped();
        }

        protected void PauseGame() => GameMode.PauseGame();
        protected void ResumeGame() => GameMode.ResumeGame();

        public virtual void PossessPawn(ULevelPawn pawn, bool overrideCurrentPawn = false)
        {
            if (pawn != null)
            {
                pawn.OnPossess(this);

                if (overrideCurrentPawn)
                {
                    possessedPawn = pawn;
                }
            }
        }


        protected virtual void UnPossessPawn() 
        {
            if (possessedPawn != null)
            {
                possessedPawn.OnUnPossess(this);

                possessedPawn = null;
            }
        }

        public virtual void ChangePossession(ULevelPawn pawn)
        {
            UnPossessPawn();
            PossessPawn(pawn, true);
        }

        public T GetPossessedPawn<T>() where T : ULevelPawn
        {
            return possessedPawn as T;
        }
    }

}