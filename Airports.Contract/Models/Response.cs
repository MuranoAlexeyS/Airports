namespace Airports.Contract.Models
{
    public struct Response<V>
    {
        public Response(ResponseState state, V data)
        {
            _state = state;
            _data = data;
        }

        private V _data { get; set; }
        private ResponseState _state;
        public ResponseState GetState() {
            return _state;
        }
        public V GetData() {
            return _data;
        }
    }
}
