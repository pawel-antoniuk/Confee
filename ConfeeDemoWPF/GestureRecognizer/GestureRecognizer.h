// GestureRecognizer.h

#pragma once

using namespace System;

namespace Confee {

	public ref class GestureRecognizer
	{
	public:
		void Process(array<float> ^polygon); 
	};
}
