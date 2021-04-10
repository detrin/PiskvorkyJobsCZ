using System;

namespace PiskvorkyJobsCZ
{
    class InvalidMoveException : Exception
    {
        const string notEvenMessage = "This move is not allowed, check values in the board. ";

        public InvalidMoveException( ) :
            base( notEvenMessage )
        { }

        public InvalidMoveException( string auxMessage ) :
            base( String.Format( "{0} - {1}",
                auxMessage, notEvenMessage ) )
        { }
    }
}


