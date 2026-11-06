namespace AgentToolComponents.Models.RazorGen;

public static class RazorSampleProjects
{
    //public static List<ProjectFile> IntroProject =>
    //[
    //    new ProjectFile
    //    {
    //        Name = RazorConstants.DefaultComponentName,
    //        FileType = FileType.Razor,
    //        Content = """
    //                  <div class="text-center">
    //                      <h1>Hello Party People!</h1>
    //                      <h2>This is your App Builder sample app</h2>
    //                      <h3>@testString</h3>
    //                      <button class="btn btn-primary" @onclick="GetTestString">Test</button>
    //                      <Child ChangeParent="HandleChildString" />
    //                  </div>
    //                  <button @onclick=@(() => showDice = !showDice)>Toggle to @(showDice ? "TestData.razor" : "Die20.razor")</button>
    //                  <hr />
    //                  @if(showDice)
    //                  {
    //                      <Die20/>
    //                  }
    //                  <TestData />
    //                  @code
    //                  {
    //                      string testString = "I Am Here!";
    //                      bool showDice;
    //                      void GetTestString()
    //                      {
    //                          testString = testString == Test.GetString() ? "I Am Here" : Test.GetString();
    //                      }
    //                      void HandleChildString(string str)
    //                      {
    //                         testString = str;
    //                      }    
    //                  }
    //                  """
    //    },

    //    new ProjectFile
    //    {
    //        Name = "Child.razor",
    //        FileType = FileType.Razor,
    //        Content = """
    //                  <button class="btn btn-primary" @onclick="SayHello">Child Button</button>

    //                  @code {
    //                      [Parameter]
    //                      public EventCallback<string> ChangeParent {get;set;}
    //                      void SayHello()
    //                      {
    //                          ChangeParent.InvokeAsync("Hello From Child Component");
    //                      }

    //                  }
    //                  """
    //    },

    //    new ProjectFile
    //    {
    //        Name = "Test.cs",
    //        FileType = FileType.Class,
    //        Content = """
    //                  public class Test
    //                  {
    //                      public Test(string title, string topic, int difficulty)
    //                      {
    //                          Title = title;
    //                          Topic = topic;
    //                          Difficulty = difficulty;
    //                      }
    //                      public string Title {get;set;}
    //                      public string Topic {get;set;}
    //                      public int Difficulty {get;set;}
    //                  	public static string GetString()
    //                      {
    //                          return "Hello From Test Class";
    //                      }
    //                  }
    //                  """
    //    },

    //    new ProjectFile
    //    {
    //        Name = "TestData.razor",
    //        FileType = FileType.Razor,
    //        Content = """
    //                  <h3 class="text-center">TestData.razor</h3>
    //                  <div class="text-center">
    //                      <p style="font-size:.8rem">click header to sort</p>
    //                  <table style="margin:auto">
    //                      <tr>
    //                          <th @onclick="@(() => ChangeOrder("Title"))">Title</th>
    //                          <th @onclick="@(() => ChangeOrder("Topic"))">Topic</th>
    //                          <th @onclick="@(() => ChangeOrder("Difficulty"))">Difficulty</th>
    //                      </tr>
    //                      @foreach (var test in tests)
    //                      {
    //                          <tr>
    //                              <td>@test.Title</td>
    //                              <td>@test.Topic</td>
    //                              <td>@test.Difficulty</td>
    //                          </tr>
    //                      }
    //                  </table>
    //                  </div>

    //                  @code{

    //                      List<Test> tests = new List<Test>
    //                          {
    //                              new Test("Algebra", "Math",5),
    //                              new Test("Dickins", "Literature", 4),
    //                              new Test("Physics", "Science", 9),
    //                              new Test("US States", "Geography", 2),
    //                              new Test("CSharp", "Programming", 1),
    //                              new Test("Algebra2", "Math",7),
    //                              new Test("Faulkner", "Literature", 6),
    //                              new Test("Physics2", "Science", 10),
    //                              new Test("Country Capitols", "Geography", 5),
    //                              new Test("C# Razor", "Programming", 1)
    //                          };
    //                      private bool isDsc;
    //                      private void ChangeOrder(string orderByCol)
    //                      {
    //                          if (isDsc)
    //                          {
    //                              tests = orderByCol switch
    //                              {
    //                                  "Title" => tests.OrderBy(x => x.Title).ToList(),
    //                                  "Topic" => tests.OrderBy(x => x.Topic).ToList(),
    //                                  _ => tests.OrderBy(x => x.Difficulty).ToList()
    //                              };
    //                          }
    //                          else
    //                          {
    //                              tests = orderByCol switch
    //                              {
    //                                  "Title" => tests.OrderByDescending(x => x.Title).ToList(),
    //                                  "Topic" => tests.OrderByDescending(x => x.Topic).ToList(),
    //                                  _ => tests.OrderByDescending(x => x.Difficulty).ToList()
    //                              };
    //                          }

    //                          isDsc = !isDsc;
    //                          StateHasChanged();
    //                      }
                          
    //                  }
    //                  """
    //    }
    //];

}