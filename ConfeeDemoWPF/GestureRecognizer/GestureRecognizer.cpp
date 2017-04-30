// This is the main DLL file.

#include "stdafx.h"

#include "GestureRecognizer.h"

using namespace boost::geometry;

void Confee::GestureRecognizer::Process(array<float>^ polygons)
{
	auto polygonsSize = polygons->Length;
	pin_ptr<float> polygonsPinPtr = &polygons[0];
	float *polygonsPtr = polygonsPinPtr;

	model::polygon<model::d2::point_xy<float>> poly;
	append(poly, polygonsPtr);
}
