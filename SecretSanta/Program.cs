using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SecretSanta
{
    class Program
    {
        public static IConfigurationRoot Configuration { get; set; }
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.AddUserSecrets<Program>();
            Configuration = builder.Build();

            var people = new List<Person>()
            {
                new Person(0, "Kid 1", 1, "email1@test.com", "Description"),
                new Person(1, "Kid 2",1, "email1@test.com", "Description"),
                new Person(2, "Kid 3",2, "email2@test.com", "Description"),
                new Person(3, "Kid 4",2, "email2@test.com", "Description"),
                new Person(4, "Kid 5",3, "email2@test.com", "Description"),
                new Person(5, "Kid 6",3, "email2@test.com", "Description"),
            };

            var graph = new SSGraph(people);

            // Walk our list randomly
            var r = new Random();
            foreach (var person in people.OrderBy(x => r.Next()))
            {
                // Loop through each person, randomly select an available edge, and then remove other edges
                var destinations = graph.GetDestinations(person.Index);
                var selectedDestination = destinations[r.Next(destinations.Count)];

                // Clean up our graph to remove illegal edges
                // This method will return an index if we accidentally choose a path that removes the last out edge for another edge.
                if (destinations.Count == 1)
                {
                    graph.RemoveAllOtherIncomingEdgesToDestination(selectedDestination, person.Index);
                }
                else
                {
                    while (graph.RemoveAllOtherIncomingEdgesToDestination(selectedDestination, person.Index) != -1)
                    {
                        selectedDestination = destinations[r.Next(destinations.Count)];
                    }
                }
                
                graph.RemoveAllOtherOutgoingEdgesFromSource(person.Index, selectedDestination);

                // We have selected a specific destination
                person.Selected = people.FirstOrDefault(x => x.Index == selectedDestination);
            }

            if(people.Any(x=>x.Selected==null))
                throw new Exception("😥");

            foreach (var person in people)
            {
                Console.WriteLine($"{person} -> {person.Selected}");
            }

            //foreach (var person in people)
            //{
            //    SendEmail(person);
            //}

        }

        /*
        * Send assignment
        */
        static void SendEmail(Person p)
        {
            var emailLogin = Configuration.GetSection("EmailLogin").Value;
            var emailPass = Configuration.GetSection("EmailPass").Value;

            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(emailLogin, emailPass),
                EnableSsl = true
            };

            var body = $"{p.Name}'s assignment is: {p.Selected.Name} <br><br>{p.Selected.Description}";
            var message=new MailMessage("SecretSantaThrowAway1234@gmail.com", p.Email, $"Secret Santa Assignment For {p.Name}", body);
            message.IsBodyHtml = true;
            client.Send(message);
        }
    }

    public class Person
    {
        public Person Selected { get; set; }
        public int Index { get; }
        public int Family { get; }
        public string Email { get; }
        public string Description { get; }
        public string Name { get; }
        public Person(int index, string name, int family, string email, string description)
        {
            Index = index;
            Family = family;
            Email = email;
            Description = description;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    // Stores our secret santa graph as an adjacency matrix. Removes sibling edges on load. 
    public class SSGraph
    {
        private readonly bool[][] _edges;
        public SSGraph(List<Person> people)
        {
            //Create edges connecting all others
            _edges = new bool[people.Count][];
            for (var i = 0; i < people.Count; i++)
            {
                _edges[i]=new bool[people.Count];
                Array.Fill(_edges[i], true);
            }

            //Remove siblings - n^2
            foreach (var family in people.GroupBy(x=>x.Family).Select(x=>x.ToList()))
            {
                for (var i = 0; i < family.Count; i++)
                {
                    //Remove edge to yourself
                    _edges[family[i].Index][family[i].Index] = false;

                    for (var j = i+1; j < family.Count; j++)
                    {
                        //Remove all edges between i and j
                        _edges[family[i].Index][family[j].Index] = false;
                        _edges[family[j].Index][family[i].Index] = false;
                    }
                }
            }
        }
        public override string ToString()
        {
            var b=new StringBuilder();
            foreach (var row in _edges)
            {
                b.Append(string.Join(",", row));
                b.Append("\n");
            }
            return b.ToString();
        }
        public List<int> GetDestinations(in int personIndex)
        {
            var result=new List<int>();
            for (var i = 0; i < _edges[personIndex].Length; i++)
            {
                if(_edges[personIndex][i])
                    result.Add(i);
            }
            return result;
        }

        public int RemoveAllOtherIncomingEdgesToDestination(in int destination, in int source)
        {
            // Run a first pass and return if performing this will cause another node to have 0 out-degree
            for (var i = 0; i < _edges.Length; i++)
            {
                if (i == source) continue;
                _edges[i][destination] = false;
                var noEdges=Array.TrueForAll(_edges[i], x => !x);
                _edges[i][destination] = true;
                if (noEdges)
                    return i;
            }

            // Remove edge coming from i, heading to destination, except for i
            for (var i = 0; i < _edges.Length; i++)
            {
                if (i != source)
                    _edges[i][destination] = false;
            }

            return -1;
        }
        public void RemoveAllOtherOutgoingEdgesFromSource(in int source, in int destination)
        {
            for (var i = 0; i < _edges[source].Length; i++)
            {
                if (i != destination)
                    _edges[source][i] = false;
            }
        }
    }
}
