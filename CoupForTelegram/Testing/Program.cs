using System;
using System.Collections.Generic;
using System.Linq;

namespace Testing
{
    class Program
    {
        static List<Card> Cards;
        static void Main(string[] args)
        {
            BatchTest();
            var run = Enumerable.Range(0, 100).Select(x => BatchTest()).ToList();
            Console.Read();

        }

        static bool BatchTest()
        {
            Cards = CardHelper.GenerateCards();

            Cards.Shuffle();
            Cards.RemoveRange(0, 10);

            var p1 = Cards.Take(2).ToList();
            foreach (var c in p1)
            {
                Cards.Remove(c);
                //Console.WriteLine(c.Name);
            }
            //run 100 tests
            var results = Enumerable.Range(0, 100).Select(x => RunTest(p1));
            Console.WriteLine("Number of times same card was given back: " + results.Count(x => x));
            return true;
        }

        static bool RunTest(List<Card> p1)
        {
            var cardUsed = p1.First().Name;
            //pretend to lose / gain new card
            var card = p1.First(x => x.Name == cardUsed);
            var old = card.Name;
            Cards.Add(card);
            p1.Remove(card);
            Cards.Shuffle();
            card = Cards.First();
            Cards.Remove(card);
            p1.Add(card);
            var newC = card.Name;
            return old == newC;
        }
    }
}
