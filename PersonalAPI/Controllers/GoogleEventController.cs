﻿using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using PersonalAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace PersonalAPI.Controllers
{
    [Route("/")]
    [ApiController]
    public class GoogleEventController : ControllerBase
    {
                
        [HttpGet("available-hour-blocks")]
        [Authorize]
        public async Task<IActionResult> GetAvailableHourBlocks()
        {
            
            string calendarID = "your-id";

            string[] Scopes = { CalendarService.Scope.Calendar };
            GoogleCredential credentials;
            using (var stream = new FileStream("service_key.json", FileMode.Open, FileAccess.Read))
            {

                credentials = GoogleCredential.FromStream(stream)
                                     .CreateScoped(Scopes);
            }

            var calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials,
                ApplicationName = "Google Calendar API Sample",
            });

            DateTime start = WholeHourModel.RoundUpToNextWholeHour(DateTime.Now);
            DateTime end = start.AddDays(7); // Adjust as needed for the range you want to check
            int blockSizeInMinutes = 60;

            var freeBusyRequest = new FreeBusyRequest
            {
                TimeMin = start,
                TimeMax = end,
                TimeZone = "EST", // Set the time zone as needed
                Items = new List<FreeBusyRequestItem> { new FreeBusyRequestItem { Id = calendarID } },
            };

            FreebusyResource.QueryRequest query = calendarService.Freebusy.Query(freeBusyRequest);
            FreeBusyResponse response = await query.ExecuteAsync();

            var availableBlocks = new List<object>();

            foreach (KeyValuePair<string, FreeBusyCalendar> calendar in response.Calendars)
            {

                IList<TimePeriod> busyTimes = calendar.Value.Busy;
                DateTime current = start;

                while (current < end)
                {
                    DateTime next = current.AddMinutes(blockSizeInMinutes);
                    TimePeriod conflict = null;

                    foreach (TimePeriod busy in busyTimes)
                    {
                        DateTime busyStart = Convert.ToDateTime(busy.Start);
                        DateTime busyEnd = Convert.ToDateTime(busy.End);

                        if ((current >= busyStart && current < busyEnd) || (next > busyStart && next <= busyEnd))
                        {
                            conflict = busy;
                            break;
                        }
                    }

                    if (conflict == null)
                    {
                        availableBlocks.Add(new { Start = current });
                        current = next;
                    }
                    else
                    {
                        current = Convert.ToDateTime(conflict.End);

                    }
                }
            }

            return Ok(availableBlocks);
        }

        // POST api/<GoogleEventController>

        [HttpPost("schedule")]
        public async Task<IActionResult> Scheduler([FromBody] EventRequestModel eventRequest )
        {
            
            string calendarID = "your-id";

            string[] Scopes = { CalendarService.Scope.Calendar };
            GoogleCredential credentials;
            using (var stream = new FileStream("service_key.json", FileMode.Open, FileAccess.Read))
            {

                credentials = GoogleCredential.FromStream(stream)
                                     .CreateScoped(Scopes);
            }

            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
                ApplicationName = "Consult Software Guy"
            });
            // The above code creates a new CalendarService instance using the authorized credentials.
            // The application name specified in the initializer is used to identify your application in the Google API Console.

            var newEvent = new Event()
            {
                Summary = eventRequest.Summary,
                Location = eventRequest.Location,
                Start = new EventDateTime()
                {
                    DateTime = eventRequest.Start,                    
                },
                End = new EventDateTime()
                {
                    DateTime = eventRequest.Start.AddMinutes(60),
                },
                Description = eventRequest.Description,
            };

            // Check for time conflicts
            EventsResource.ListRequest request = service.Events.List(calendarID);
            request.TimeMin = newEvent.Start.DateTime;
            request.TimeMax = newEvent.End.DateTime;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            

            // Insert the new event
            EventsResource.InsertRequest insertRequest = service.Events.Insert(newEvent, calendarID);
            var createdEvent = await service.Events.Insert(newEvent, calendarID).ExecuteAsync();

            if (createdEvent != null)
            {
                return CreatedAtAction(nameof(Scheduler), eventRequest);
            }
            else
            {
                return BadRequest("Error creating event.");
            }

        }        
    }    
}
