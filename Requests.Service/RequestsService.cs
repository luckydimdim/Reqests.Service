using AutoMapper;
using Cmas.Infrastructure.Domain.Commands;
using Cmas.Infrastructure.Domain.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Cmas.BusinessLayers.Requests;
using Cmas.BusinessLayers.CallOffOrders;
using Cmas.BusinessLayers.TimeSheets;
using Cmas.BusinessLayers.Contracts;
using Cmas.BusinessLayers.Requests.Entities;
using Microsoft.Extensions.Logging;
using Cmas.Services.Requests.Dtos;
using Cmas.Infrastructure.ErrorHandler;
using Cmas.BusinessLayers.TimeSheets.Entities;
using Cmas.BusinessLayers.CallOffOrders.Entities;

namespace Cmas.Services.Requests
{
    public class RequestsService
    {
        private readonly RequestsBusinessLayer _requestsBusinessLayer;
        private readonly CallOffOrdersBusinessLayer _callOffOrdersBusinessLayer;
        private readonly ContractBusinessLayer _contractBusinessLayer;
        private readonly TimeSheetsBusinessLayer _timeSheetsBusinessLayer;
        private readonly IMapper _autoMapper;
        private ILogger _logger;

        public RequestsService(IServiceProvider serviceProvider)
        {
            var _commandBuilder = (ICommandBuilder) serviceProvider.GetService(typeof(ICommandBuilder));
            var _queryBuilder = (IQueryBuilder) serviceProvider.GetService(typeof(IQueryBuilder));
            var _loggerFactory = (ILoggerFactory) serviceProvider.GetService(typeof(ILoggerFactory));

            _autoMapper = (IMapper) serviceProvider.GetService(typeof(IMapper));
            _logger = _loggerFactory.CreateLogger<RequestsService>();

            _callOffOrdersBusinessLayer = new CallOffOrdersBusinessLayer(_commandBuilder, _queryBuilder);
            _contractBusinessLayer = new ContractBusinessLayer(_commandBuilder, _queryBuilder);
            _timeSheetsBusinessLayer = new TimeSheetsBusinessLayer(_commandBuilder, _queryBuilder);
            _requestsBusinessLayer = new RequestsBusinessLayer(_commandBuilder, _queryBuilder);
        }

        public async Task<string> DeleteRequestAsync(string requestId)
        {
            // удаляем табели

            IEnumerable<string> ids = await _timeSheetsBusinessLayer.GetTimeSheetsByRequestId(requestId);

            foreach (var id in ids)
            {
                await _timeSheetsBusinessLayer.DeleteTimeSheet(id);
            }

            return await _requestsBusinessLayer.DeleteRequest(requestId);
        }

        /// <summary>
        /// Получить название статуса.
        /// TODO: Перенести в класс - локализатор
        /// </summary>
        private string GetRequestStatusName(RequestStatus status)
        {
            switch (status)
            {
                case RequestStatus.Creation:
                    return "Не заполнена";
                case RequestStatus.Validation:
                    return "На проверке";
                case RequestStatus.Correction:
                    return "Содержит ошибки";
                case RequestStatus.Done:
                    return "Проверена";
                default:
                    return "";
            }
        }

        /// <summary>
        ///  
        /// </summary>
        private async Task<IEnumerable<TimeSheetDto>> GetTimeSheets(IEnumerable<string> callOffOrderIds,
            string requestId)
        {
            var result = new List<TimeSheetDto>();

            foreach (var callOffOrderId in callOffOrderIds)
            {
                var callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);

                TimeSheet timeSheet = null;
                try
                {
                    timeSheet =
                        await _timeSheetsBusinessLayer.GetTimeSheetByCallOffOrderAndRequest(callOffOrderId, requestId);
                }
                catch (NotFoundErrorException exc)
                {
                    _logger.Log(LogLevel.Warning, (EventId) 0,
                        String.Format("Time sheet by call-off order {0} and request {1} not found", callOffOrderId,
                            requestId), exc,
                        (state, error) => state.ToString());
                    continue;
                }


                var timeSheetDto = new TimeSheetDto();
                timeSheetDto.Id = timeSheet.Id;
                timeSheetDto.Assignee = callOffOrder.Assignee;
                timeSheetDto.CreatedAt = timeSheet.CreatedAt;
                timeSheetDto.UpdatedAt = timeSheet.CreatedAt;
                timeSheetDto.Name = callOffOrder.Name;
                timeSheetDto.Amount = timeSheet.Amount;
                timeSheetDto.Position = callOffOrder.Position;
                timeSheetDto.StatusName = TimeSheetsBusinessLayer.GetStatusName(timeSheet.Status);
                timeSheetDto.StatusSysName = timeSheet.Status.ToString();

                result.Add(timeSheetDto);
            }

            return result;
        }

        public async Task<IEnumerable<string>> CreateTimeSheetsAsync(string requestId,
            IEnumerable<string> callOffOrderIds)
        {
            var createdTimeSheets = new List<string>();

            foreach (var callOffOrderId in callOffOrderIds)
            {
                CallOffOrder callOffOrder = await _callOffOrdersBusinessLayer.GetCallOffOrder(callOffOrderId);
                IEnumerable<TimeSheet> timeSheets =
                    await _timeSheetsBusinessLayer.GetTimeSheetsByCallOffOrderId(callOffOrderId);

                // FIXME: Изменить после преобразования из string в DateTime
                DateTime startDate = DateTime.ParseExact(callOffOrder.StartDate, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture);

                // FIXME: Изменить после преобразования из string в DateTime
                DateTime finishDate = DateTime.ParseExact(callOffOrder.FinishDate, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture);

                string timeSheetId = null;
                bool created = false;
                while (startDate < finishDate)
                {
                    var tsExist =
                        timeSheets.Where(
                                ts =>
                                    (ts.Month == startDate.Month &&
                                     ts.Year == startDate.Year))
                            .Any();

                    if (tsExist)
                    {
                        startDate = startDate.AddMonths(1);
                        continue;
                    }
                    else
                    {
                        timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                            startDate.Month, startDate.Year, requestId);
                        created = true;
                        break;
                    }
                }

                if (!created)
                {
                    timeSheetId = await _timeSheetsBusinessLayer.CreateTimeSheet(callOffOrderId,
                        finishDate.Month, finishDate.Year, requestId);
                }

                createdTimeSheets.Add(timeSheetId);
            }

            return createdTimeSheets;
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(string requestId)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            return await GetDetailedRequest(request);
        }

        private async Task<DetailedRequestDto> GetDetailedRequest(Request request)
        {
            DetailedRequestDto result = _autoMapper.Map<DetailedRequestDto>(request);

            var contract = await _contractBusinessLayer.GetContract(request.ContractId);

            result.Documents = await GetTimeSheets(request.CallOffOrderIds, request.Id);

            result.Summary.WorksQuantity = request.CallOffOrderIds.Count;
            result.Summary.WorksAmount = result.Documents.Sum(doc => doc.Amount);
            result.Summary.Total = result.Summary.WorksAmount;
            result.Summary.Amount = result.Summary.WorksAmount;

            if (contract.VatIncluded)
            {
                result.Summary.Vat = Math.Round((result.Summary.WorksAmount / 1.18 - result.Summary.WorksAmount) * -1);
            }

            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<SimpleRequestDto> GetSimpleRequest(Request request)
        {
            var result = _autoMapper.Map<SimpleRequestDto>(request);


            var contract = await _contractBusinessLayer.GetContract(result.ContractId);
            result.ContractNumber = contract.Number;
            result.ContractorName = contract.ContractorName;

            result.StatusName = GetRequestStatusName(request.Status);
            result.StatusSysName = request.Status.ToString();

            return result;
        }

        private async Task<IEnumerable<SimpleRequestDto>> GetSimpleRequests(IEnumerable<Request> requests)
        {
            var result = new List<SimpleRequestDto>();

            foreach (var request in requests)
            {
                SimpleRequestDto simpleRequest = null;
                try
                {
                    simpleRequest = await GetSimpleRequest(request);
                }
                catch (NotFoundErrorException exc)
                {
                    _logger.Log(LogLevel.Warning, (EventId) 0,
                        String.Format("Contract {0} not found", request.ContractId), exc,
                        (state, error) => state.ToString());
                    continue;
                }

                result.Add(simpleRequest);
            }

            return result;
        }

        public async Task<DetailedRequestDto> UpdateRequestStatusHandlerAsync(string requestId, RequestStatus status)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            if (request.Status == status)
                return await GetDetailedRequest(request);

            // проверки смены статуса

            if (request.Status == RequestStatus.Done)
                throw new Exception("Cannot change status of the request with status 'Done'");


            if (status == RequestStatus.Creation)
                throw new Exception("Cannot change status of the request to 'Creation'");

            request.Status = status;

            await _requestsBusinessLayer.UpdateRequest(request);

            return await GetDetailedRequest(request);
        }

        public async Task<DetailedRequestDto> UpdateRequestAsync(string requestId, IList<string> callOffOrderIds)
        {
            Request request = await _requestsBusinessLayer.GetRequest(requestId);

            request.CallOffOrderIds = callOffOrderIds;

            await _requestsBusinessLayer.UpdateRequest(request);

            return await GetDetailedRequest(request);
        }

        public async Task<DetailedRequestDto> CreateRequestAsync(CreateRequestDto request)
        {
            string requestId = null;
            IEnumerable<string> createdTimeSheetIds = null;
            try
            {
                requestId = await _requestsBusinessLayer.CreateRequest(request.ContractId,
                    request.CallOffOrderIds);

                createdTimeSheetIds = await CreateTimeSheetsAsync(requestId, request.CallOffOrderIds);
                return await GetDetailedRequest(requestId);
            }
            catch (Exception exc)
            {
                if (!string.IsNullOrEmpty(requestId))
                {
                    await _requestsBusinessLayer.DeleteRequest(requestId);

                    if (createdTimeSheetIds != null)
                    {
                        foreach (var timeSheetId in createdTimeSheetIds)
                        {
                            await _timeSheetsBusinessLayer.DeleteTimeSheet(timeSheetId);
                        }
                    }
                }

                throw exc;
            }
        }

        public async Task<DetailedRequestDto> GetRequestAsync(string requestId)
        {
            return await GetDetailedRequest(requestId);
        }

        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsAsync()
        {
            var result = await _requestsBusinessLayer.GetRequests();

            return await GetSimpleRequests(result);
        }

        public async Task<IEnumerable<SimpleRequestDto>> GetRequestsByContractAsync(string contractId)
        {
            var result = await _requestsBusinessLayer.GetRequestsByContractId(contractId);

            return await GetSimpleRequests(result);
        }
    }
}