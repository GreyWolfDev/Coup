using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Testing
{
    public class Card
    {
        /// <summary>
        /// Name of the card to display
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// What actions this card is allowed to use
        /// </summary>
        public List<Action> ActionsAllowed { get; set; } = new List<Action> { Action.Income, Action.Coup, Action.ForeignAid }; //Seed with default actions all players are allowed to use
    }

    public class Duke : Card
    {
        public Duke()
        {
            ActionsAllowed.AddRange(new[] { Action.BlockForeignAid, Action.Tax });
            Name = "💰 Duke";
        }
    }

    public class Contessa : Card
    {
        public Contessa()
        {
            Name = "👠 Contessa";
            ActionsAllowed.AddRange(new[] { Action.BlockAssassinate });
        }
    }

    public class Ambassador : Card
    {
        public Ambassador()
        {
            Name = "👳 Ambassador";
            ActionsAllowed.AddRange(new[] { Action.Exchange, Action.BlockSteal });
        }
    }

    public class Captain : Card
    {
        public Captain()
        {
            Name = "🛡 Captain";
            ActionsAllowed.AddRange(new[] { Action.Steal, Action.BlockSteal });
        }
    }

    public class Assassin : Card
    {
        public Assassin()
        {
            Name = "💀 Assassin";
            ActionsAllowed.AddRange(new[] { Action.Assassinate });
        }
    }

    public static class CardHelper
    {
        public static List<Card> GenerateCards()
        {
            var result = new List<Card> { new Duke(), new Duke(), new Duke(), new Contessa(), new Contessa(), new Contessa(),
                new Captain(), new Captain(), new Captain(), new Ambassador(), new Ambassador(), new Ambassador(),
                new Assassin(), new Assassin(), new Assassin()};
            result.Shuffle();
            result.Shuffle();
            return result;
        }
    }

    public enum Action
    {
        /// <summary>
        /// Take 1 coin
        /// </summary>
        Income,
        /// <summary>
        /// Take 2 coins. Can be blocked
        /// </summary>
        ForeignAid,
        /// <summary>
        /// Pay 7 coins, coup another player (they lose a card) - Unblockable
        /// </summary>
        Coup,
        /// <summary>
        /// Take 3 coins.
        /// </summary>
        Tax,
        /// <summary>
        /// Pay 3 coins. Assassinate another player
        /// </summary>
        Assassinate,
        /// <summary>
        /// Exchange cards with deck
        /// </summary>
        Exchange,
        /// <summary>
        /// Steal 2 coins from another player
        /// </summary>
        Steal,
        /// <summary>
        /// Block stealing
        /// </summary>
        BlockSteal,
        /// <summary>
        /// Block asssassination
        /// </summary>
        BlockAssassinate,
        /// <summary>
        /// Block foreign aid
        /// </summary>
        BlockForeignAid,
        /// <summary>
        /// No choice made
        /// </summary>
        None,
        Concede
    }
}
