using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using restapi.Models;

namespace restapi.Controllers
{
    [Route("[controller]")]
    public class TimesheetsController : Controller
    {
        [HttpGet]
        [Produces(ContentTypes.Timesheets)]
        [ProducesResponseType(typeof(IEnumerable<Timecard>), 200)]
        public IEnumerable<Timecard> GetAll()
        {
            return Database
                .All
                .OrderBy(t => t.Opened);
        }

        [HttpGet("{id}")]
        [Produces(ContentTypes.Timesheet)]
        [ProducesResponseType(typeof(Timecard), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetOne(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                return Ok(timecard);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Produces(ContentTypes.Timesheet)]
        [ProducesResponseType(typeof(Timecard), 200)]
        public Timecard Create([FromBody] DocumentResource resource)
        {
            var timecard = new Timecard(resource.Resource);

            var entered = new Entered() { Resource = resource.Resource };

              timecard.Transitions.Add(new Transition(entered));

              Database.Add(timecard);

              return timecard;

            else
            {
              return NotFound();
            }
        }

        [HttpDelete("timecardId/delete/{Id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult DeleteTimeCard(string id){

          Timecard timecard = Database.Find(id);

          if(timecard.Status == TimecardStatus.Cancelled && timecard.Status == TimecardStatus.Draft)
          {
              return StatusCode(409, new InvalidStateError() { });
          }

          if (timecard == null)
          {
            return NotFound();
          }

          var transition = new Transition(deleted, TimecardStatus.Deleted);

          timecard.Transition.Add(transition);

          Database.Remove(timecard);

          return Ok();
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult DeleteLine(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard == null)
            {
                return NotFound();
            }

            if (timecard.CanBeDeleted() == false)
            {
                    return StatusCode(409, new InvalidStateError() { });
            }

            Database.Delete(id);
            return Ok();
        }

        [HttpGet("{id}/lines")]
        [Produces(ContentTypes.TimesheetLines)]
        [ProducesResponseType(typeof(IEnumerable<AnnotatedTimecardLine>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetLines(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                var lines = timecard.Lines
                    .OrderBy(l => l.WorkDate)
                    .ThenBy(l => l.Recorded);

                return Ok(lines);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/lines")]
        [Produces(ContentTypes.TimesheetLine)]
        [ProducesResponseType(typeof(AnnotatedTimecardLine), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        public IActionResult AddLine(string id, [FromBody] TimecardLine timecardLine)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null && timecard.Resource == timecardLine.Resource)
            {
                if (timecard.Status != TimecardStatus.Draft)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                var annotatedLine = timecard.AddLine(timecardLine);

                return Ok(annotatedLine);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{timecardId}/lines/{lineId}")]
        public IActionResult ReplaceLine(string timecardId, Guid lineId, [FromBody] TimecardLine timecardLine)
        {
            Timecard timecard = Database.Find(timecardId);

            if (timecard == null)
            {
                return NotFound();
            }

            if (timecard.HasLine(lineId) == false)
            {
                // this might be better served by using some other 4xx error
                // because there's actually a problem with both the resource
                // we're updating and the request
                return NotFound();
            }

            if (timecard.Resource != timecardLine.Resource)
            {
                return NotFound();
            }

            var result = timecard.ReplaceLine(lineId, timecardLine);
            return Ok(result);
        }

        [HttpPatch("{timecardId}/lines/{lineId}")]
        public IActionResult UpdateLine(string timecardId, Guid lineId, [FromBody] dynamic timecardLine)
        {
            Timecard timecard = Database.Find(timecardId);

            if (timecard == null)
            {
                return NotFound();
            }

            if (timecard.HasLine(lineId) == false)
            {
                // this might be better served by using some other 4xx error
                // because there's actually a problem with both the resource
                // we're updating and the request
                return NotFound();
            }

            var result = timecard.ReplaceLine(lineId, timecardLine);
            return Ok(result);
        }

        [HttpGet("{id}/transitions")]
        [Produces(ContentTypes.Transitions)]
        [ProducesResponseType(typeof(IEnumerable<Transition>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetTransitions(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                return Ok(timecard.Transitions);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/submittal")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Submit(string id, [FromBody] Submittal submittal)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null && timecard.Resource == submittal.Resource)
            {
                if (timecard.Status != TimecardStatus.Draft)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                if (timecard.Lines.Count < 1)
                {
                    return StatusCode(409, new EmptyTimecardError() { });
                }

                var transition = new Transition(submittal, TimecardStatus.Submitted);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/submittal")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetSubmittal(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Submitted)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Submitted)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);
                }
                else
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/cancellation")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Cancel(string id, [FromBody] Cancellation cancellation)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null && timecard.Resource != cancellation.Resource)
            {
                if (timecard.Status != TimecardStatus.Draft && timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                var transition = new Transition(cancellation, TimecardStatus.Cancelled);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/cancellation")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetCancellation(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Cancelled)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Cancelled)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);
                }
                else
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/rejection")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Close(string id, [FromBody] Rejection rejection)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null && timecard.Resource != rejection.Resource)
            {
                if (timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                var transition = new Transition(rejection, TimecardStatus.Rejected);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/rejection")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetRejection(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Rejected)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Rejected)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);
                }
                else
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/approval")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Approve(string id, [FromBody] Approval approval)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null && timecard.Resource != approval.Resource)
            {
                if (timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                var transition = new Transition(approval, TimecardStatus.Approved);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/approval")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetApproval(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Approved)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Approved)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);
                }
                else
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }
    }
}
